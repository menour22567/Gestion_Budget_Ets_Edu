using Microsoft.Data.Sqlite;

namespace PaieEducation.Tests.Integration;

/// <summary>
/// Tests du schéma V009 — Workbench réglementaire (ADR-0007, J3I §9 refactoré,
/// J3J R1-R5, J3K v1.0 — tous validés utilisateur 14-15/07/2026).
///
/// Vérifie :
///  R1 — Absence des tables de gestion (AgentAttributs, AgentRubriques,
///       AvertissementsHistorique) en V009 : décision historique, non
///       testable ici depuis que V011 les crée (<c>CreateMigrated()</c>
///       applique toutes les migrations). Leur présence effective est
///       vérifiée par <c>AgentCarriereSchemaTests</c> (Phase 5, jalon D).
///  R2 — PK = `Id` partout (harmonisation ADR-0004).
///  R3 — `ReglesEligibilite.Critere` (TEXT + CHECK) SUPPRIMÉ au profit de
///       `CritereId` (FK vers CriteresEligibilite.Id) — source unique.
///  R4 révisé — Audit minimal sur les catalogues techniques (SourcesValeur,
///       CriteresEligibilite) ; audit complet préservé sur MessagesRegles
///       (texte réglementaire) et GroupesEligibilite (règle réglementaire).
///  R5 — YAGNI : pas de table/colonne créés sans cas d'usage.
///
/// Le test d'upgrade V008→V009 est exercé par <see cref="MigratorTests"/> via
/// <c>CreateMigrated()</c> qui applique toutes les migrations en séquence.
/// </summary>
public class WorkbenchReglementaireSchemaTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IReadOnlyList<string> TableColumns(SqliteConnection c, string table)
    {
        var cols = new List<string>();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info('{table}');";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            cols.Add(rdr.GetString(rdr.GetOrdinal("name")));
        }
        return cols;
    }

    private static long Count(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void InsertRubriqueMin(SqliteConnection c, string id)
    {
        SchemaTestSupport.Exec(c, """
            INSERT INTO Rubriques
                (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul, CreatedAt, Hash)
            VALUES ($id, 'Test', 'GAIN', 'TRAITEMENT', 'MENSUELLE', 1, '2026-01-01T00:00:00Z', 'h');
            """, ("$id", id));
    }

    // =========================================================================
    // R1 — Tables de gestion NON créées en V009 (décision historique).
    // Non testable via CreateMigrated() depuis que V011 les crée (Phase 5,
    // jalon D) : voir AgentCarriereSchemaTests pour la vérification positive.
    // =========================================================================

    // =========================================================================
    // R2 — PK = `Id` partout (pas `Code`) sur les nouveaux catalogues
    // =========================================================================

    [Theory]
    [InlineData("SourcesValeur")]
    [InlineData("CriteresEligibilite")]
    [InlineData("MessagesRegles")]
    [InlineData("GroupesEligibilite")]
    public void R2_PK_principale_est_Id_pas_Code(string table)
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var cols = TableColumns(scope.Conn, table);
        Assert.Contains("Id", cols);
        Assert.DoesNotContain("Code", cols);
    }

    // =========================================================================
    // Nouvelles tables existent + seeds corrects
    // =========================================================================

    [Fact]
    public void Tables_SourcesValeur_CriteresEligibilite_MessagesRegles_GroupesEligibilite_existent()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        foreach (var table in new[] { "SourcesValeur", "CriteresEligibilite", "MessagesRegles", "GroupesEligibilite" })
        {
            var exists = (long)SchemaTestSupport.Scalar<long>(scope.Conn,
                $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}';") == 1L;
            Assert.True(exists, $"Table {table} manquante");
        }
    }

    [Fact]
    public void Seed_SourcesValeur_contient_7_codes_attendus()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        Assert.Equal(7L, Count(scope.Conn, "SELECT COUNT(*) FROM SourcesValeur;"));
        // Quelques codes clés du seed
        Assert.Equal(1L, Count(scope.Conn, "SELECT COUNT(*) FROM SourcesValeur WHERE Id='NOTATION_AGENT';"));
        Assert.Equal(1L, Count(scope.Conn, "SELECT COUNT(*) FROM SourcesValeur WHERE Id='POINT_INDICIAIRE';"));
        Assert.Equal(1L, Count(scope.Conn, "SELECT COUNT(*) FROM SourcesValeur WHERE Id='CONSTANTE_REGLEMENTAIRE';"));
    }

    [Fact]
    public void Seed_CriteresEligibilite_contient_10_codes_attendus()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        Assert.Equal(10L, Count(scope.Conn, "SELECT COUNT(*) FROM CriteresEligibilite;"));
        // Codes clés D3
        Assert.Equal(1L, Count(scope.Conn, "SELECT COUNT(*) FROM CriteresEligibilite WHERE Id='CORPS';"));
        Assert.Equal(1L, Count(scope.Conn, "SELECT COUNT(*) FROM CriteresEligibilite WHERE Id='ORIGINE_STATUTAIRE';"));
        Assert.Equal(1L, Count(scope.Conn, "SELECT COUNT(*) FROM CriteresEligibilite WHERE Id='TYPE_ETABLISSEMENT';"));
    }

    [Fact]
    public void Seed_MessagesRegles_et_GroupesEligibilite_sont_vides()
    {
        // Les catalogues de messages et de groupes sont gérés par le Workbench (D7)
        // au fil des besoins — le seed V009 n'insère rien dedans.
        using var scope = SchemaTestSupport.CreateMigrated();
        Assert.Equal(0L, Count(scope.Conn, "SELECT COUNT(*) FROM MessagesRegles;"));
        Assert.Equal(0L, Count(scope.Conn, "SELECT COUNT(*) FROM GroupesEligibilite;"));
    }

    // =========================================================================
    // R3 — `ReglesEligibilite.CritereId` FK remplace `Critere` TEXT + CHECK
    // =========================================================================

    [Fact]
    public void R3_ReglesEligibilite_a_CritereId_pas_Critere()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var cols = TableColumns(scope.Conn, "ReglesEligibilite");
        Assert.Contains("CritereId", cols);
        Assert.DoesNotContain("Critere", cols);
    }

    [Fact]
    public void R3_ReglesEligibilite_aussi_GroupeId_DNF()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var cols = TableColumns(scope.Conn, "ReglesEligibilite");
        Assert.Contains("GroupeId", cols);
    }

    [Fact]
    public void R3_insertion_ReglesEligibilite_exige_CritereId_valide()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertRubriqueMin(scope.Conn, "R-X");

        // CritereId inexistant : la FK refuse.
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO ReglesEligibilite
                (Id, RubriqueId, CritereId, Operateur, Valeur, DateEffet, Hash, CreatedAt)
            VALUES ('RE-BAD', 'R-X', 'CRITERE_INEXISTANT', '=', 'X', '2025-01-01', 'h', '2026-01-01T00:00:00Z');
            """));
        Assert.Contains("FOREIGN KEY", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void R3_insertion_ReglesEligibilite_avec_CritereId_valide_reussit()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertRubriqueMin(scope.Conn, "ISSRP_45");

        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO ReglesEligibilite
                (Id, RubriqueId, CritereId, Operateur, Valeur, DateEffet, Hash, CreatedAt)
            VALUES ('RE-1', 'ISSRP_45', 'CORPS', '=', 'PEM', '2025-01-01', 'h', '2026-01-01T00:00:00Z');
            """);
        Assert.Equal(1L, Count(scope.Conn, "SELECT COUNT(*) FROM ReglesEligibilite WHERE CritereId='CORPS';"));
    }

    [Fact]
    public void R3_plus_de_CHECK_sur_Critere_puisque_colonne_supprimee()
    {
        // Si le CHECK avait survécu (par erreur), l'insertion avec une valeur arbitraire
        // serait rejetée. On vérifie que CritereId accepte n'importe quel Id du dictionnaire
        // (le CHECK, s'il existe, refuserait 'CORPS' alors qu'il est valide).
        // Ici on confirme surtout qu'on peut insérer avec CritereId='CORPS' sans erreur.
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertRubriqueMin(scope.Conn, "R-OK");
        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO ReglesEligibilite
                (Id, RubriqueId, CritereId, Operateur, Valeur, DateEffet, Hash, CreatedAt)
            VALUES ('RE-OK', 'R-OK', 'CORPS', 'IN', 'PEM,PES,INSPECTION', '2025-01-01', 'h', '2026-01-01T00:00:00Z');
            """);
        Assert.Equal(1L, Count(scope.Conn, "SELECT COUNT(*) FROM ReglesEligibilite;"));
    }

    // =========================================================================
    // R4 révisé — Audit : catalogues techniques (minimal) vs réglementaire (complet)
    // =========================================================================

    [Theory]
    [InlineData("SourcesValeur")]
    [InlineData("CriteresEligibilite")]
    public void R4_catalogues_techniques_audit_minimal_sans_DateEffet_DateFin_Source_Hash(string table)
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var cols = TableColumns(scope.Conn, table);
        Assert.DoesNotContain("DateEffet", cols);
        Assert.DoesNotContain("DateFin", cols);
        Assert.DoesNotContain("Source", cols);
        Assert.DoesNotContain("Hash", cols);
        // Audit minimal : Actif + CreatedAt + CreatedBy obligatoires
        Assert.Contains("Actif", cols);
        Assert.Contains("CreatedAt", cols);
        Assert.Contains("CreatedBy", cols);
    }

    [Fact]
    public void R4_MessagesRegles_texte_reglementaire_audit_complet()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var cols = TableColumns(scope.Conn, "MessagesRegles");
        // Audit complet préservé
        Assert.Contains("DateEffet", cols);
        Assert.Contains("DateFin", cols);
        Assert.Contains("Source", cols);
        Assert.Contains("Actif", cols);
        Assert.Contains("CreatedAt", cols);
        Assert.Contains("CreatedBy", cols);
    }

    [Fact]
    public void R4_GroupesEligibilite_regle_reglementaire_audit_complet()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var cols = TableColumns(scope.Conn, "GroupesEligibilite");
        Assert.Contains("DateEffet", cols);
        Assert.Contains("DateFin", cols);
        Assert.Contains("Source", cols);
        Assert.Contains("Hash", cols);
    }

    // =========================================================================
    // L-M1 — Rubriques.SourceValeurId
    // =========================================================================

    [Fact]
    public void L_M1_Rubriques_a_SourceValeurId_nullable_par_defaut()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var cols = TableColumns(scope.Conn, "Rubriques");
        Assert.Contains("SourceValeurId", cols);

        // Insertion sans SourceValeurId : NULL par défaut
        InsertRubriqueMin(scope.Conn, "R-NULL");
        var src = SchemaTestSupport.Scalar<string?>(scope.Conn,
            "SELECT SourceValeurId FROM Rubriques WHERE Id='R-NULL';");
        Assert.Null(src);
    }

    [Fact]
    public void L_M1_SourceValeurId_reference_une_source_existante()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertRubriqueMin(scope.Conn, "PAPP");

        // PAPP utilise NOTATION_AGENT (seed existant).
        SchemaTestSupport.Exec(scope.Conn, """
            UPDATE Rubriques SET SourceValeurId='NOTATION_AGENT' WHERE Id='PAPP';
            """);
        Assert.Equal("NOTATION_AGENT", SchemaTestSupport.Scalar<string>(scope.Conn,
            "SELECT SourceValeurId FROM Rubriques WHERE Id='PAPP';"));
    }

    [Fact]
    public void L_M1_SourceValeurId_inconnu_rejete_par_FK()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertRubriqueMin(scope.Conn, "R-BAD");

        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn, """
            UPDATE Rubriques SET SourceValeurId='SOURCE_INEXISTANTE' WHERE Id='R-BAD';
            """));
        Assert.Contains("FOREIGN KEY", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // L-M3 — RubriqueBaremes.UpdatedAt / UpdatedBy
    // =========================================================================

    [Fact]
    public void L_M3_RubriqueBaremes_a_UpdatedAt_UpdatedBy_nullables()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var cols = TableColumns(scope.Conn, "RubriqueBaremes");
        Assert.Contains("UpdatedAt", cols);
        Assert.Contains("UpdatedBy", cols);

        // Une ligne fraîche : UpdatedAt = NULL
        InsertRubriqueMin(scope.Conn, "IFC");
        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO RubriqueBaremes
                (Id, RubriqueId, Dimension, BorneInf, BorneSup, TypeValeur, Valeur,
                 DateEffet, Hash, CreatedAt)
            VALUES ('RB-1', 'IFC', 'CATEGORIE', '1', '17', 'MONTANT', '1500',
                    '2008-01-01', 'h', '2026-01-01T00:00:00Z');
            """);
        var updAt = SchemaTestSupport.Scalar<string?>(scope.Conn,
            "SELECT UpdatedAt FROM RubriqueBaremes WHERE Id='RB-1';");
        Assert.Null(updAt);
    }

    // =========================================================================
    // Index
    // =========================================================================

    [Fact]
    public void Index_GroupesEligibilite_et_ReglesEligibilite_sont_crees()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        // IX_GroupesEligibilite_RubriqueId et IX_MessageId
        var idxCount = Count(scope.Conn, """
            SELECT COUNT(*) FROM sqlite_master
            WHERE type='index' AND name IN
              ('IX_GroupesEligibilite_RubriqueId',
               'IX_GroupesEligibilite_MessageId',
               'IX_ReglesEligibilite_CritereId',
               'IX_ReglesEligibilite_GroupeId',
               'IX_Rubriques_SourceValeurId');
            """);
        Assert.Equal(5L, idxCount);
    }
}
