using Microsoft.Data.Sqlite;

namespace PaieEducation.Tests.Integration;

/// <summary>
/// Tests du schéma V011 — Agents, Carrière, Période (Phase 5, jalon D).
///
/// Vérifie :
///  D-A — Agents ne porte aucune colonne de carrière (Grade/Corps/Échelon...).
///  D-B — Une seule table Carrieres (poste + affectation fusionnés).
///  Contraintes : Matricule UNIQUE, CHECK Sexe/Statut/TypeContrat/Etat/Decision,
///  FK Carrieres → Agents/Grades/Categories/Echelons, unicité (Agent, DateEffet)
///  et (Année, Mois) pour Periodes.
///  D-D — AgentAttributs/AgentRubriques/AgentRubriqueParametres/
///  AvertissementsHistorique existent (DDL portée depuis J3H §4/7/8).
/// </summary>
public class AgentCarriereSchemaTests
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

    private static void SeedReferentiel(SqliteConnection c)
    {
        SchemaTestSupport.Exec(c, """
            INSERT INTO Filieres (Id, Libelle, CreatedAt, Hash) VALUES ('ENSEIGNANT', 'Enseignant', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ('PEM', 'Prof. École', 'ENSEIGNANT', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('PEM-G1', 'Grade 1', 'PEM', 1, '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('PEM-G2', 'Grade 2', 'PEM', 2, '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('13', 13, 'Catégorie 13', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('5', 5, 'Échelon 5', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Fonctions (Id, Libelle, CreatedAt, Hash) VALUES ('DIRECTEUR', 'Directeur', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Etablissements (Id, Nom, Type, CreatedAt, Hash) VALUES ('LYC001', 'Lycée test', 'LYCEE', '2026-01-01T00:00:00Z', 'h');
            """);
    }

    private static void SeedAgent(SqliteConnection c, string id, string matricule)
    {
        SchemaTestSupport.Exec(c, """
            INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, CreatedAt)
            VALUES ($id, $matricule, 'Test', 'Agent', '1990-01-01', '2015-09-01', 'M', '2026-01-01T00:00:00Z');
            """, ("$id", id), ("$matricule", matricule));
    }

    // =========================================================================
    // Tables existent
    // =========================================================================

    [Theory]
    [InlineData("Agents")]
    [InlineData("Carrieres")]
    [InlineData("Periodes")]
    [InlineData("AgentAttributs")]
    [InlineData("AgentRubriques")]
    [InlineData("AgentRubriqueParametres")]
    [InlineData("AvertissementsHistorique")]
    public void Table_existe(string table)
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var exists = SchemaTestSupport.Scalar<long>(scope.Conn,
            $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}';") == 1L;
        Assert.True(exists, $"Table {table} manquante");
    }

    // =========================================================================
    // D-A — Agents = identité pure, aucune colonne de carrière
    // =========================================================================

    [Fact]
    public void D_A_Agents_ne_porte_aucune_colonne_de_carriere()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var cols = TableColumns(scope.Conn, "Agents");
        foreach (var interdite in new[] { "GradeId", "CorpsId", "CategorieId", "EchelonId", "Echelon", "FonctionId" })
            Assert.DoesNotContain(interdite, cols);
        Assert.DoesNotContain("Actif", cols); // Statut porte déjà cette information
    }

    [Fact]
    public void Agents_Matricule_est_unique()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedAgent(scope.Conn, "A-1", "MAT-001");

        var ex = Assert.Throws<SqliteException>(() => SeedAgent(scope.Conn, "A-2", "MAT-001"));
        Assert.Contains("UNIQUE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Agents_Sexe_hors_domaine_est_rejete()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, CreatedAt)
            VALUES ('A-BAD', 'MAT-BAD', 'Test', 'Agent', '1990-01-01', '2015-09-01', 'X', '2026-01-01T00:00:00Z');
            """));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Agents_Statut_par_defaut_est_ACTIF()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedAgent(scope.Conn, "A-1", "MAT-001");
        Assert.Equal("ACTIF", SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT Statut FROM Agents WHERE Id='A-1';"));
    }

    // =========================================================================
    // D-B — Carrieres : une seule table (poste + affectation), FK, unicité
    // =========================================================================

    [Fact]
    public void Carrieres_porte_poste_et_affectation_dans_la_meme_ligne()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var cols = TableColumns(scope.Conn, "Carrieres");
        foreach (var attendue in new[] { "GradeId", "CategorieId", "EchelonId", "FonctionId", "TypeContrat", "EtablissementId" })
            Assert.Contains(attendue, cols);
    }

    [Fact]
    public void Carrieres_GradeId_inconnu_rejete_par_FK()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SchemaTestSupport.Exec(scope.Conn, "PRAGMA foreign_keys=ON;");
        SeedReferentiel(scope.Conn);
        SeedAgent(scope.Conn, "A-1", "MAT-001");

        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO Carrieres (Id, AgentId, GradeId, CategorieId, EchelonId, TypeContrat, DateEffet, Motif, CreatedAt)
            VALUES ('C-1', 'A-1', 'GRADE_INEXISTANT', '13', '5', 'STATUTAIRE', '2015-09-01', 'Recrutement', '2026-01-01T00:00:00Z');
            """));
        Assert.Contains("FOREIGN KEY", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Carrieres_insertion_nominale_reussit()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SchemaTestSupport.Exec(scope.Conn, "PRAGMA foreign_keys=ON;");
        SeedReferentiel(scope.Conn);
        SeedAgent(scope.Conn, "A-1", "MAT-001");

        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO Carrieres
                (Id, AgentId, GradeId, CategorieId, EchelonId, FonctionId, TypeContrat, EtablissementId, DateEffet, Motif, CreatedAt)
            VALUES
                ('C-1', 'A-1', 'PEM-G1', '13', '5', 'DIRECTEUR', 'STATUTAIRE', 'LYC001', '2015-09-01', 'Recrutement', '2026-01-01T00:00:00Z');
            """);
        Assert.Equal(1L, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM Carrieres;"));
    }

    [Fact]
    public void Carrieres_deux_lignes_meme_agent_meme_DateEffet_rejetees()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SchemaTestSupport.Exec(scope.Conn, "PRAGMA foreign_keys=ON;");
        SeedReferentiel(scope.Conn);
        SeedAgent(scope.Conn, "A-1", "MAT-001");
        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO Carrieres (Id, AgentId, GradeId, CategorieId, EchelonId, TypeContrat, DateEffet, Motif, CreatedAt)
            VALUES ('C-1', 'A-1', 'PEM-G1', '13', '5', 'STATUTAIRE', '2015-09-01', 'Recrutement', '2026-01-01T00:00:00Z');
            """);

        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO Carrieres (Id, AgentId, GradeId, CategorieId, EchelonId, TypeContrat, DateEffet, Motif, CreatedAt)
            VALUES ('C-2', 'A-1', 'PEM-G2', '13', '5', 'STATUTAIRE', '2015-09-01', 'Doublon', '2026-01-01T00:00:00Z');
            """));
        Assert.Contains("UNIQUE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Carrieres_TypeContrat_hors_domaine_est_rejete()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SchemaTestSupport.Exec(scope.Conn, "PRAGMA foreign_keys=ON;");
        SeedReferentiel(scope.Conn);
        SeedAgent(scope.Conn, "A-1", "MAT-001");

        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO Carrieres (Id, AgentId, GradeId, CategorieId, EchelonId, TypeContrat, DateEffet, Motif, CreatedAt)
            VALUES ('C-BAD', 'A-1', 'PEM-G1', '13', '5', 'BENEVOLE', '2015-09-01', 'Recrutement', '2026-01-01T00:00:00Z');
            """));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // Periodes — cycle de vie ADR-0008
    // =========================================================================

    [Fact]
    public void Periodes_Etat_par_defaut_est_OUVERTE()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO Periodes (Id, Annee, Mois, DateOuverture, CreatedAt)
            VALUES ('2025-06', 2025, 6, '2025-06-01T00:00:00Z', '2026-01-01T00:00:00Z');
            """);
        Assert.Equal("OUVERTE", SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT Etat FROM Periodes WHERE Id='2025-06';"));
    }

    [Fact]
    public void Periodes_Etat_hors_domaine_est_rejete()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO Periodes (Id, Annee, Mois, Etat, DateOuverture, CreatedAt)
            VALUES ('2025-06', 2025, 6, 'INVENTEE', '2025-06-01T00:00:00Z', '2026-01-01T00:00:00Z');
            """));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Periodes_Annee_Mois_est_unique()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO Periodes (Id, Annee, Mois, DateOuverture, CreatedAt)
            VALUES ('2025-06', 2025, 6, '2025-06-01T00:00:00Z', '2026-01-01T00:00:00Z');
            """);
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO Periodes (Id, Annee, Mois, DateOuverture, CreatedAt)
            VALUES ('2025-06-bis', 2025, 6, '2025-06-01T00:00:00Z', '2026-01-01T00:00:00Z');
            """));
        Assert.Contains("UNIQUE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // D-D — AgentAttributs / AgentRubriques / AvertissementsHistorique (J3H)
    // =========================================================================

    [Fact]
    public void AgentAttributs_deux_lignes_meme_cle_meme_DateEffet_rejetees()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedAgent(scope.Conn, "A-1", "MAT-001");
        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO AgentAttributs (Id, AgentId, Attribut, Valeur, DateEffet, CreatedAt)
            VALUES ('AA-1', 'A-1', 'ORIGINE_STATUTAIRE', 'ENSEIGNANT', '2025-01-01', '2026-01-01T00:00:00Z');
            """);
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO AgentAttributs (Id, AgentId, Attribut, Valeur, DateEffet, CreatedAt)
            VALUES ('AA-2', 'A-1', 'ORIGINE_STATUTAIRE', 'AUTRE', '2025-01-01', '2026-01-01T00:00:00Z');
            """));
        Assert.Contains("UNIQUE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentRubriques_Statut_hors_domaine_est_rejete()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SchemaTestSupport.Exec(scope.Conn, "PRAGMA foreign_keys=ON;");
        SeedAgent(scope.Conn, "A-1", "MAT-001");
        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO Rubriques (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul, CreatedAt, Hash)
            VALUES ('QUALIF', 'Qualification', 'GAIN', 'TRAITEMENT', 'MENSUELLE', 1, '2026-01-01T00:00:00Z', 'h');
            """);
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO AgentRubriques (Id, AgentId, RubriqueId, Statut, Origine, DateEffet, CreatedAt)
            VALUES ('AR-1', 'A-1', 'QUALIF', 'INVENTE', 'MANUELLE', '2025-01-01', '2026-01-01T00:00:00Z');
            """));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AvertissementsHistorique_Decision_hors_domaine_est_rejete()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SchemaTestSupport.Exec(scope.Conn, "PRAGMA foreign_keys=ON;");
        SeedAgent(scope.Conn, "A-1", "MAT-001");
        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO Rubriques (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul, CreatedAt, Hash)
            VALUES ('QUALIF', 'Qualification', 'GAIN', 'TRAITEMENT', 'MENSUELLE', 1, '2026-01-01T00:00:00Z', 'h');
            """);
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO AvertissementsHistorique
                (Id, EmisLe, AgentId, RubriqueId, Severite, MessageAffiche, Decision, CreatedAt)
            VALUES ('AH-1', '2026-01-01T00:00:00Z', 'A-1', 'QUALIF', 'INFO', 'Test', 'INVENTEE', '2026-01-01T00:00:00Z');
            """));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
