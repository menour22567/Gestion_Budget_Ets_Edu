using Microsoft.Data.Sqlite;
using PaieEducation.Persistence.Migrations;

namespace PaieEducation.Tests.Integration;

/// <summary>
/// Tests d'intégration du schéma V005 — Éligibilité des rubriques et
/// cotisations paramétrables.
/// Couvre :
///   - création des 3 tables ;
///   - CHECK sur Critere / Operateur / TypeCotisation / AssietteRef ;
///   - UNIQUE Cotisations(Code, DateEffet), CotisationAssietteRubriques(...);
///   - FK CotisationAssietteRubriques → Cotisations, Rubriques ;
///   - cas métier SS 9 % (Q3b) ;
///   - 3 ISSRP à 45 / 30 / 15 % (éligibilité par corps).
/// </summary>
public class EligibiliteCotisationsSchemaTests
{
    private static readonly string[] V005Tables =
    {
        "ReglesEligibilite", "Cotisations", "CotisationAssietteRubriques"
    };

    // -----------------------------------------------------------------------
    // Tests structurels
    // -----------------------------------------------------------------------

    [Fact]
    public void V005_est_chargee_comme_ressource()
    {
        var names = typeof(SqliteMigrator).Assembly.GetManifestResourceNames();
        Assert.Contains($"{SchemaTestSupport.ResourcePrefix}V005__eligibilite_cotisations.sql", names);
    }

    [Fact]
    public void Apres_V005_les_3_tables_existent()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var existing = TableNames(scope.Conn);
        foreach (var t in V005Tables)
            Assert.Contains(t, existing);
    }

    [Fact]
    public void SchemaVersions_trace_V005()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var found = SchemaTestSupport.Scalar<long>(scope.Conn,
            "SELECT COUNT(*) FROM SchemaVersions WHERE Version = 5 AND Name = 'eligibilite_cotisations';");
        Assert.Equal(1L, found);
    }

    // -----------------------------------------------------------------------
    // CHECK constraints
    // -----------------------------------------------------------------------

    [Fact]
    public void CHECK_ReglesEligibilite_Critere_rejette_valeur_inconnue()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertRubrique(scope.Conn, "IEP", "IEP");
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO ReglesEligibilite (Id, RubriqueId, Critere, Operateur, Valeur, DateEffet, CreatedAt, Hash) " +
            "VALUES ('RE-1', 'IEP', 'N_IMPORTE_QUOI', '=', 'X', '2007-01-01', '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CHECK_ReglesEligibilite_Operateur_rejette_valeur_inconnue()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertRubrique(scope.Conn, "IEP", "IEP");
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO ReglesEligibilite (Id, RubriqueId, Critere, Operateur, Valeur, DateEffet, CreatedAt, Hash) " +
            "VALUES ('RE-1', 'IEP', 'CORPS', '~', 'X', '2007-01-01', '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CHECK_Cotisations_TypeCotisation_et_AssietteRef_rejettent_valeurs_inconnues()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO Cotisations (Id, Code, Libelle, TypeCotisation, AssietteRef, DateEffet, CreatedAt, Hash) " +
            "VALUES ('X', 'X', 'X', 'N_IMPORTE_QUOI', 'ASSIETTE_COTISABLE', '2007-01-01', '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);

        var ex2 = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO Cotisations (Id, Code, Libelle, TypeCotisation, AssietteRef, DateEffet, CreatedAt, Hash) " +
            "VALUES ('Y', 'Y', 'Y', 'OBLIGATOIRE_SALARIALE', 'N_IMPORTE_QUOI', '2007-01-01', '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("CHECK", ex2.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CHECK_Cotisations_Taux_borne_0_a_1_quand_non_null()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO Cotisations (Id, Code, Libelle, TypeCotisation, Taux, AssietteRef, DateEffet, CreatedAt, Hash) " +
            "VALUES ('X', 'X', 'X', 'OBLIGATOIRE_SALARIALE', 1.5, 'ASSIETTE_COTISABLE', '2007-01-01', '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // UNIQUE
    // -----------------------------------------------------------------------

    [Fact]
    public void UNIQUE_Cotisations_Code_DateEffet_pas_de_doublon()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertCotisation(scope.Conn, "SS-1", "SS", "Sécurité sociale", "OBLIGATOIRE_SALARIALE", 0.09, "2007-01-01");
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO Cotisations (Id, Code, Libelle, TypeCotisation, Taux, AssietteRef, DateEffet, CreatedAt, Hash) " +
            "VALUES ('SS-2', 'SS', 'Sécurité sociale v2', 'OBLIGATOIRE_SALARIALE', 0.10, 'ASSIETTE_COTISABLE', '2007-01-01', '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("UNIQUE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UNIQUE_CotisationAssietteRubriques_pas_de_doublon_sur_la_meme_periode()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertRubrique(scope.Conn, "TBASE", "Traitement de base");
        InsertCotisation(scope.Conn, "SS-1", "SS", "Sécurité sociale", "OBLIGATOIRE_SALARIALE", 0.09, "2007-01-01");
        SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO CotisationAssietteRubriques (Id, CotisationId, RubriqueId, DateEffet, CreatedAt, Hash) " +
            "VALUES ('AR-1', 'SS-1', 'TBASE', '2007-01-01', '2026-01-01T00:00:00Z', 'h');");

        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO CotisationAssietteRubriques (Id, CotisationId, RubriqueId, DateEffet, CreatedAt, Hash) " +
            "VALUES ('AR-DUP', 'SS-1', 'TBASE', '2007-01-01', '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("UNIQUE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FK_CotisationAssietteRubriques_vers_Cotisations_et_Rubriques()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO CotisationAssietteRubriques (Id, CotisationId, RubriqueId, DateEffet, CreatedAt, Hash) " +
            "VALUES ('AR-1', 'COT-INEX', 'RUB-INEX', '2007-01-01', '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("FOREIGN KEY", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // Cas métier : SS 9 % (Q3b)
    // -----------------------------------------------------------------------

    [Fact]
    public void Cas_metier_SS_9_pourcent_est_inscriptible_et_lisible()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertCotisation(scope.Conn, "SS-2007", "SS", "Sécurité sociale (part ouvrière)",
            "OBLIGATOIRE_SALARIALE", 0.09, "2007-01-01");

        var taux = SchemaTestSupport.Scalar<double>(scope.Conn,
            "SELECT Taux FROM Cotisations WHERE Code = 'SS' AND DateEffet = '2007-01-01';");
        Assert.Equal(0.09, taux);
    }

    [Fact]
    public void Cas_metier_une_cotisation_facultative_a_montant_fixe_n_exige_pas_de_taux()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        // Mutuelle : Taux = null, AssietteRef = MONTANT_FIXE.
        SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO Cotisations (Id, Code, Libelle, TypeCotisation, Taux, AssietteRef, EstRetenue, DateEffet, CreatedAt, Hash) " +
            "VALUES ('MUT-1', 'MUTUELLE', 'Mutuelle (facultative)', 'FACULTATIVE', NULL, 'MONTANT_FIXE', 1, '2007-01-01', '2026-01-01T00:00:00Z', 'h');");

        var type = SchemaTestSupport.Scalar<string>(scope.Conn,
            "SELECT TypeCotisation FROM Cotisations WHERE Code = 'MUTUELLE';");
        Assert.Equal("FACULTATIVE", type);
    }

    // -----------------------------------------------------------------------
    // Cas métier : ISSRP à 3 taux (éligibilité par corps)
    // -----------------------------------------------------------------------

    [Fact]
    public void Cas_metier_ISSRP_3_taux_eligibles_selon_corps()
    {
        using var scope = SchemaTestSupport.CreateMigrated();

        // 3 rubriques distinctes (une par taux), chacune avec sa règle d'éligibilité.
        InsertRubrique(scope.Conn, "ISSRP_45", "Soutien scolaire 45 %");
        InsertRubrique(scope.Conn, "ISSRP_30", "Soutien scolaire 30 %");
        InsertRubrique(scope.Conn, "ISSRP_15", "Soutien scolaire 15 %");

        InsertRegleEligibilite(scope.Conn, "RE-ISSRP-45", "ISSRP_45", "CORPS", "=", "PEM", "2007-01-01");
        InsertRegleEligibilite(scope.Conn, "RE-ISSRP-30", "ISSRP_30", "CORPS", "=", "PELP", "2007-01-01");
        InsertRegleEligibilite(scope.Conn, "RE-ISSRP-15", "ISSRP_15", "CORPS", "=", "AT", "2007-01-01");

        // Le moteur de paie demandera, pour un agent en corps PEM, la rubrique
        // ISSRP éligible. On vérifie ici la requête brute :
        //   WHERE RubriqueId = ... AND Critere = 'CORPS' AND Operateur = '=' AND Valeur = 'PEM'
        var eligiblePourPem = SchemaTestSupport.Scalar<string>(scope.Conn,
            "SELECT RubriqueId FROM ReglesEligibilite " +
            "WHERE Critere = 'CORPS' AND Operateur = '=' AND Valeur = 'PEM';");
        Assert.Equal("ISSRP_45", eligiblePourPem);

        var eligiblePourPelp = SchemaTestSupport.Scalar<string>(scope.Conn,
            "SELECT RubriqueId FROM ReglesEligibilite " +
            "WHERE Critere = 'CORPS' AND Operateur = '=' AND Valeur = 'PELP';");
        Assert.Equal("ISSRP_30", eligiblePourPelp);

        var eligiblePourAt = SchemaTestSupport.Scalar<string>(scope.Conn,
            "SELECT RubriqueId FROM ReglesEligibilite " +
            "WHERE Critere = 'CORPS' AND Operateur = '=' AND Valeur = 'AT';");
        Assert.Equal("ISSRP_15", eligiblePourAt);
    }

    [Fact]
    public void Cas_metier_regle_avec_Operateur_IN_sur_liste_de_corps()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertRubrique(scope.Conn, "ISSRP_45", "Soutien scolaire 45 %");
        // Groupe pédagogique élargi : PEM, PES, INSPECTION, etc.
        InsertRegleEligibilite(scope.Conn, "RE-ISSRP-45", "ISSRP_45", "CORPS", "IN",
            "PEM,PES,INSPECTION,DIRECTEUR,CENSEUR", "2007-01-01");

        // La requête « la valeur contient PEM » fonctionne avec LIKE + délimiteurs virgule.
        // C'est la base de la résolution multi-valeurs que le moteur utilisera.
        var match = SchemaTestSupport.Scalar<int>(scope.Conn,
            "SELECT COUNT(*) FROM ReglesEligibilite " +
            "WHERE Critere = 'CORPS' AND Operateur = 'IN' " +
            "  AND (',' || Valeur || ',') LIKE ('%,' || $v || ',%');",
            ("$v", "PEM"));
        Assert.Equal(1, match);

        var noMatch = SchemaTestSupport.Scalar<int>(scope.Conn,
            "SELECT COUNT(*) FROM ReglesEligibilite " +
            "WHERE Critere = 'CORPS' AND Operateur = 'IN' " +
            "  AND (',' || Valeur || ',') LIKE ('%,' || $v || ',%');",
            ("$v", "PEMX"));
        Assert.Equal(0, noMatch);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static HashSet<string> TableNames(SqliteConnection conn)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) set.Add(reader.GetString(0));
        return set;
    }

    private static void InsertRubrique(SqliteConnection c, string id, string libelle)
    {
        SchemaTestSupport.Exec(c,
            "INSERT INTO Rubriques (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul, CreatedAt, Hash) " +
            "VALUES ($id, $l, 'GAIN', 'FORFAIT', 'MENSUELLE', 1, $t, $h);",
            ("$id", id), ("$l", libelle), ("$t", "2026-01-01T00:00:00Z"), ("$h", $"h-{id}"));
    }

    private static void InsertCotisation(
        SqliteConnection c, string id, string code, string libelle,
        string type, double? taux, string dateEffet)
    {
        SchemaTestSupport.Exec(c,
            "INSERT INTO Cotisations (Id, Code, Libelle, TypeCotisation, Taux, AssietteRef, DateEffet, CreatedAt, Hash) " +
            "VALUES ($id, $c, $l, $t, $tx, 'ASSIETTE_COTISABLE', $de, $ts, $h);",
            ("$id", id), ("$c", code), ("$l", libelle), ("$t", type),
            ("$tx", (object?)taux ?? DBNull.Value), ("$de", dateEffet),
            ("$ts", "2026-01-01T00:00:00Z"), ("$h", $"h-{id}"));
    }

    private static void InsertRegleEligibilite(
        SqliteConnection c, string id, string rubriqueId, string critere, string operateur,
        string valeur, string dateEffet)
    {
        SchemaTestSupport.Exec(c,
            "INSERT INTO ReglesEligibilite (Id, RubriqueId, Critere, Operateur, Valeur, DateEffet, CreatedAt, Hash) " +
            "VALUES ($id, $r, $cr, $op, $v, $de, $ts, $h);",
            ("$id", id), ("$r", rubriqueId), ("$cr", critere), ("$op", operateur),
            ("$v", valeur), ("$de", dateEffet), ("$ts", "2026-01-01T00:00:00Z"),
            ("$h", $"h-{id}"));
    }
}
