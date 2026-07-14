using Microsoft.Data.Sqlite;
using PaieEducation.Persistence.Migrations;

namespace PaieEducation.Tests.Integration;

/// <summary>
/// Tests d'intégration du schéma V004 — Rubriques, RubriqueFormules,
/// RubriqueParametres, RubriqueDependances.
/// </summary>
public class RubriquesSchemaTests
{
    // Tables introduites par V004.
    private static readonly string[] V004Tables =
    {
        "Rubriques", "RubriqueFormules", "RubriqueParametres", "RubriqueDependances"
    };

    // -----------------------------------------------------------------------
    // Tests structurels
    // -----------------------------------------------------------------------

    [Fact]
    public void V004_est_chargee_comme_ressource_de_l_assembly()
    {
        var names = typeof(SqliteMigrator).Assembly.GetManifestResourceNames();
        Assert.Contains($"{SchemaTestSupport.ResourcePrefix}V004__rubriques.sql", names);
    }

    [Fact]
    public void Apres_V004_les_4_tables_rubriques_existent()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var existing = TableNames(scope.Conn);

        foreach (var t in V004Tables)
            Assert.Contains(t, existing);
    }

    [Fact]
    public void SchemaVersions_trace_V004()
    {
        using var scope = SchemaTestSupport.CreateMigrated();

        var found = SchemaTestSupport.Scalar<long>(scope.Conn,
            "SELECT COUNT(*) FROM SchemaVersions WHERE Version = 4 AND Name = 'rubriques';");
        Assert.Equal(1L, found);
    }

    // -----------------------------------------------------------------------
    // CHECK constraints sur les énumérations
    // -----------------------------------------------------------------------

    [Fact]
    public void CHECK_Rubriques_Nature_accepte_seulement_GAIN_RETENUE_COTISATION_IMPOT()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO Rubriques (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul, CreatedAt, Hash) " +
            "VALUES ('X', 'X', 'BIDON', 'FORFAIT', 'MENSUELLE', 1, '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CHECK_Rubriques_BaseCalcul_rejette_valeur_inconnue()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO Rubriques (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul, CreatedAt, Hash) " +
            "VALUES ('X', 'X', 'GAIN', 'INVENTE', 'MENSUELLE', 1, '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CHECK_Rubriques_EstImposable_et_EstCotisable_accepte_seulement_0_ou_1()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO Rubriques (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul, EstImposable, CreatedAt, Hash) " +
            "VALUES ('X', 'X', 'GAIN', 'FORFAIT', 'MENSUELLE', 1, 7, '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // UNIQUE & FK
    // -----------------------------------------------------------------------

    [Fact]
    public void UNIQUE_RubriqueFormules_Rubrique_DateEffet_pas_de_doublon_de_version()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertRubrique(scope.Conn, "IEP", "IEP", "GAIN", "TBASE", "MENSUELLE", 10);
        InsertFormule(scope.Conn, "RF-IEP-1", "IEP", "2007-01-01", "round(TBASE * TAUX * ECH, 2)");

        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO RubriqueFormules (Id, RubriqueId, DateEffet, Expression, CreatedAt, Hash) " +
            "VALUES ('RF-IEP-DUP', 'IEP', '2007-01-01', 'X', '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("UNIQUE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UNIQUE_RubriqueParametres_Rubrique_Cle_DateEffet_pas_de_doublon()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertRubrique(scope.Conn, "IEP", "IEP", "GAIN", "TBASE", "MENSUELLE", 10);
        InsertParametre(scope.Conn, "RP-IEP-1", "IEP", "TAUX", "2007-01-01", "0.04");

        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO RubriqueParametres (Id, RubriqueId, Cle, DateEffet, Valeur, CreatedAt, Hash) " +
            "VALUES ('RP-DUP', 'IEP', 'TAUX', '2007-01-01', '0.05', '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("UNIQUE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UNIQUE_RubriqueDependances_pas_de_doublon_et_pas_d_auto_dependance()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertRubrique(scope.Conn, "A", "A", "GAIN", "TBASE", "MENSUELLE", 10);
        InsertRubrique(scope.Conn, "B", "B", "GAIN", "TBASE", "MENSUELLE", 20);
        SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO RubriqueDependances (Id, RubriqueId, DependDeId, DateEffet, CreatedAt, Hash) " +
            "VALUES ('RD-1', 'A', 'B', '2007-01-01', '2026-01-01T00:00:00Z', 'h');");

        // Doublon
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO RubriqueDependances (Id, RubriqueId, DependDeId, DateEffet, CreatedAt, Hash) " +
            "VALUES ('RD-2', 'A', 'B', '2007-01-01', '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("UNIQUE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CHECK_RubriqueDependances_interdit_auto_dependance()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertRubrique(scope.Conn, "A", "A", "GAIN", "TBASE", "MENSUELLE", 10);
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO RubriqueDependances (Id, RubriqueId, DependDeId, DateEffet, CreatedAt, Hash) " +
            "VALUES ('RD-LOOP', 'A', 'A', '2007-01-01', '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FK_RubriqueFormules_vers_Rubriques_enforced()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO RubriqueFormules (Id, RubriqueId, DateEffet, Expression, CreatedAt, Hash) " +
            "VALUES ('RF-ORPH', 'RUB-INEXISTANTE', '2007-01-01', 'X', '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("FOREIGN KEY", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // Résolution par date (sélection d'une formule/param à une date donnée)
    // -----------------------------------------------------------------------

    [Fact]
    public void Resolution_RubriqueFormules_retourne_la_bonne_expression_a_la_date_donnee()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertRubrique(scope.Conn, "IEP", "IEP", "GAIN", "TBASE", "MENSUELLE", 10);
        InsertFormule(scope.Conn, "RF-IEP-2007", "IEP", "2007-01-01", "v1", dateFin: "2022-02-28");
        InsertFormule(scope.Conn, "RF-IEP-2022", "IEP", "2022-03-01", "v2", dateFin: null);

        Assert.Equal("v1", ResolveFormule(scope.Conn, "IEP", "2010-06-15"));
        Assert.Equal("v1", ResolveFormule(scope.Conn, "IEP", "2022-02-28"));
        Assert.Equal("v2", ResolveFormule(scope.Conn, "IEP", "2022-03-01"));
        Assert.Equal("v2", ResolveFormule(scope.Conn, "IEP", "2030-01-01"));
    }

    [Fact]
    public void Resolution_RubriqueParametres_retourne_la_bonne_valeur_a_la_date_donnee()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertRubrique(scope.Conn, "IEP", "IEP", "GAIN", "TBASE", "MENSUELLE", 10);
        InsertParametre(scope.Conn, "RP-1", "IEP", "TAUX", "2007-01-01", "0.04", dateFin: "2022-02-28");
        InsertParametre(scope.Conn, "RP-2", "IEP", "TAUX", "2022-03-01", "0.06", dateFin: null);

        Assert.Equal("0.04", ResolveParametre(scope.Conn, "IEP", "TAUX", "2010-06-15"));
        Assert.Equal("0.06", ResolveParametre(scope.Conn, "IEP", "TAUX", "2025-01-01"));
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

    private static void InsertRubrique(
        SqliteConnection c, string id, string libelle, string nature,
        string baseCalcul, string periodicite, int ordre)
    {
        SchemaTestSupport.Exec(c,
            "INSERT INTO Rubriques (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul, CreatedAt, Hash) " +
            "VALUES ($id, $l, $n, $b, $p, $o, $t, $h);",
            ("$id", id), ("$l", libelle), ("$n", nature),
            ("$b", baseCalcul), ("$p", periodicite), ("$o", ordre),
            ("$t", "2026-01-01T00:00:00Z"), ("$h", $"h-{id}"));
    }

    private static void InsertFormule(
        SqliteConnection c, string id, string rubriqueId, string dateEffet, string expression,
        string? dateFin = null)
    {
        SchemaTestSupport.Exec(c,
            "INSERT INTO RubriqueFormules (Id, RubriqueId, DateEffet, DateFin, Expression, CreatedAt, Hash) " +
            "VALUES ($id, $r, $de, $df, $e, $t, $h);",
            ("$id", id), ("$r", rubriqueId), ("$de", dateEffet),
            ("$df", (object?)dateFin ?? DBNull.Value), ("$e", expression),
            ("$t", "2026-01-01T00:00:00Z"), ("$h", $"h-{id}"));
    }

    private static void InsertParametre(
        SqliteConnection c, string id, string rubriqueId, string cle, string dateEffet, string valeur,
        string? dateFin = null)
    {
        SchemaTestSupport.Exec(c,
            "INSERT INTO RubriqueParametres (Id, RubriqueId, Cle, DateEffet, DateFin, Valeur, CreatedAt, Hash) " +
            "VALUES ($id, $r, $c, $de, $df, $v, $t, $h);",
            ("$id", id), ("$r", rubriqueId), ("$c", cle), ("$de", dateEffet),
            ("$df", (object?)dateFin ?? DBNull.Value), ("$v", valeur),
            ("$t", "2026-01-01T00:00:00Z"), ("$h", $"h-{id}"));
    }

    private static string ResolveFormule(SqliteConnection c, string rubriqueId, string date) =>
        SchemaTestSupport.Scalar<string>(c,
            "SELECT Expression FROM RubriqueFormules " +
            "WHERE RubriqueId = $r AND DateEffet <= $d AND (DateFin IS NULL OR DateFin >= $d) " +
            "ORDER BY DateEffet DESC LIMIT 1;",
            ("$r", rubriqueId), ("$d", date));

    private static string ResolveParametre(SqliteConnection c, string rubriqueId, string cle, string date) =>
        SchemaTestSupport.Scalar<string>(c,
            "SELECT Valeur FROM RubriqueParametres " +
            "WHERE RubriqueId = $r AND Cle = $c AND DateEffet <= $d AND (DateFin IS NULL OR DateFin >= $d) " +
            "ORDER BY DateEffet DESC LIMIT 1;",
            ("$r", rubriqueId), ("$c", cle), ("$d", date));
}
