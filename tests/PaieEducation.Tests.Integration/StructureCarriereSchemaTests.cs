using System.Globalization;
using Microsoft.Data.Sqlite;
using PaieEducation.Persistence.Migrations;

namespace PaieEducation.Tests.Integration;

/// <summary>
/// Tests d'intégration du schéma V002 (nomenclature) + V003 (grille indiciaire).
/// Couvre :
///   - présence des 12 tables attendues (et des 2 tables système) ;
///   - traces des 3 migrations dans SchemaVersions ;
///   - résolution par date sur ValeurPoint, GrilleIndiciaire, IndicesEchelon ;
///   - application des FOREIGN KEY et des UNIQUE ;
///   - respect des CHECK constraints (Actif, Niveau, Bornes).
/// </summary>
public class StructureCarriereSchemaTests
{
    private const string ResourcePrefix = "PaieEducation.Persistence.Migrations.";

    // Tables métier introduites par V002 (nomenclature) + V003 (grille indiciaire).
    private static readonly string[] J1bTables =
    {
        // V002 — nomenclature
        "Filieres", "TypesContrat", "TypesPersonnel", "Fonctions",
        "Echelons", "Categories", "Corps", "Grades", "Etablissements",
        // V003 — grille indiciaire
        "ValeurPoint", "GrilleIndiciaire", "IndicesEchelon"
    };

    // Tables de service ajoutées par J0/J1.a.
    private static readonly string[] SystemTables =
    {
        "SchemaVersions", "AuditLog"
    };

    // -----------------------------------------------------------------------
    // Setup
    // -----------------------------------------------------------------------

    /// <summary>
    /// Crée une base temporaire, ouvre une connexion (avec foreign_keys=ON),
    /// applique toutes les migrations en attente, et retourne un scope
    /// disposable qui libère la connexion puis le fichier temp à la fin du test.
    /// </summary>
    private static MigratedScope CreateMigratedDb()
    {
        var db = new TempSqliteDb();
        var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        Exec(conn, "PRAGMA foreign_keys=ON;");

        var migrator = new SqliteMigrator(
            new SqliteMigratorOptions(db.ConnectionString, "test"),
            MigrationLoader.LoadFromAssembly(typeof(SqliteMigrator).Assembly, ResourcePrefix));
        var result = migrator.Apply();
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Échec de la migration en setup : {result.Error}");
        }
        return new MigratedScope(db, conn);
    }

    /// <summary>Encapsule un couple (db temp file, connexion migrée) disposable.</summary>
    private sealed record MigratedScope(TempSqliteDb Db, SqliteConnection Conn) : IDisposable
    {
        public void Dispose()
        {
            Conn?.Dispose();
            Db?.Dispose();
        }
    }

    // -----------------------------------------------------------------------
    // Tests structurels
    // -----------------------------------------------------------------------

    [Fact]
    public void V002_et_V003_sont_chargees_comme_ressources_de_l_assembly()
    {
        var names = typeof(SqliteMigrator).Assembly.GetManifestResourceNames();

        Assert.Contains($"{ResourcePrefix}V002__nomenclature.sql", names);
        Assert.Contains($"{ResourcePrefix}V003__grille_indiciaire.sql", names);
    }

    [Fact]
    public void Apres_V003_la_base_contient_les_12_tables_metier_et_2_systemes()
    {
        using var scope = CreateMigratedDb();
        var conn = scope.Conn;

        var existing = new HashSet<string>(StringComparer.Ordinal);
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) existing.Add(reader.GetString(0));
        }

        foreach (var t in J1bTables)
        {
            Assert.Contains(t, existing);
        }
        foreach (var t in SystemTables)
        {
            Assert.Contains(t, existing);
        }
    }

    [Fact]
    public void SchemaVersions_contient_V001_V002_V003_apres_application()
    {
        using var scope = CreateMigratedDb();
        var conn = scope.Conn;

        // On vérifie au minimum la présence des 3 premières migrations.
        // Les ajouts ultérieurs (V004+ par J1.c) ne doivent pas casser ce test :
        // on contrôle l'existence, pas l'exclusivité.
        var v1 = SchemaTestSupport.Scalar<long>(conn, "SELECT COUNT(*) FROM SchemaVersions WHERE Version = 1;");
        var v2 = SchemaTestSupport.Scalar<long>(conn, "SELECT COUNT(*) FROM SchemaVersions WHERE Version = 2;");
        var v3 = SchemaTestSupport.Scalar<long>(conn, "SELECT COUNT(*) FROM SchemaVersions WHERE Version = 3;");
        Assert.Equal(1L, v1);
        Assert.Equal(1L, v2);
        Assert.Equal(1L, v3);
    }

    // -----------------------------------------------------------------------
    // Tests de résolution par date (cœur du moteur de paie)
    // -----------------------------------------------------------------------

    [Fact]
    public void Resolution_ValeurPoint_retourne_la_bonne_valeur_a_la_date_donnee()
    {
        using var scope = CreateMigratedDb();
        var conn = scope.Conn;

        // 45 DA depuis le 01/01/2007, officialisé au 01/03/2022 (même montant,
        // nouvelle « version » pour montrer le mécanisme de versioning).
        Exec(conn,
            "INSERT INTO ValeurPoint (Id, DateEffet, DateFin, Valeur, Version, Hash, CreatedAt) " +
            "VALUES ('VP-2007-01-01', '2007-01-01', '2022-02-28', 45, '2007', 'h1', '2026-01-01T00:00:00Z');");
        Exec(conn,
            "INSERT INTO ValeurPoint (Id, DateEffet, DateFin, Valeur, Version, Hash, CreatedAt) " +
            "VALUES ('VP-2022-03-01', '2022-03-01', NULL, 45, '2022-03', 'h2', '2026-01-01T00:00:00Z');");

        Assert.Equal(45.0, ResolveValeurPoint(conn, "2010-06-15"));
        Assert.Equal(45.0, ResolveValeurPoint(conn, "2022-02-28")); // borne haute incluse
        Assert.Equal(45.0, ResolveValeurPoint(conn, "2022-03-01")); // bascule sur la nouvelle
        Assert.Equal(45.0, ResolveValeurPoint(conn, "2030-01-01")); // DateFin NULL
    }

    [Fact]
    public void Resolution_GrilleIndiciaire_categorie_7_evolution_2007_2024()
    {
        using var scope = CreateMigratedDb();
        var conn = scope.Conn;

        // Catégorie 7 (indice 348/398/473/548 selon période).
        InsertCategorie(conn, "7", 7, "7ème catégorie", horsCategorie: false);

        InsertGrilleIndiciaire(conn, "GI-7-2007-01-01", "7", "2007-01-01", "2022-02-28", 348, "2007");
        InsertGrilleIndiciaire(conn, "GI-7-2022-03-01", "7", "2022-03-01", "2022-12-31", 398, "2022-03");
        InsertGrilleIndiciaire(conn, "GI-7-2023-01-01", "7", "2023-01-01", "2023-12-31", 473, "2023");
        InsertGrilleIndiciaire(conn, "GI-7-2024-01-01", "7", "2024-01-01", null, 548, "2024");

        Assert.Equal(348, ResolveGrilleIndiciaire(conn, "7", "2010-06-15"));
        Assert.Equal(348, ResolveGrilleIndiciaire(conn, "7", "2022-02-28"));
        Assert.Equal(398, ResolveGrilleIndiciaire(conn, "7", "2022-03-01"));
        Assert.Equal(398, ResolveGrilleIndiciaire(conn, "7", "2022-12-31"));
        Assert.Equal(473, ResolveGrilleIndiciaire(conn, "7", "2023-01-01"));
        Assert.Equal(473, ResolveGrilleIndiciaire(conn, "7", "2023-12-31"));
        Assert.Equal(548, ResolveGrilleIndiciaire(conn, "7", "2024-01-01"));
        Assert.Equal(548, ResolveGrilleIndiciaire(conn, "7", "2030-01-01"));
    }

    [Fact]
    public void Resolution_IndicesEchelon_echelon_5_evolution_2007_2022()
    {
        using var scope = CreateMigratedDb();
        var conn = scope.Conn;

        InsertEchelon(conn, "5", 5, "5ème échelon");

        Exec(conn,
            "INSERT INTO IndicesEchelon (Id, EchelonId, DateEffet, DateFin, Indice, Version, Hash, CreatedAt) " +
            "VALUES ('IE-5-2007-01-01', '5', '2007-01-01', '2022-02-28', 150, '2007', 'h', '2026-01-01T00:00:00Z');");
        Exec(conn,
            "INSERT INTO IndicesEchelon (Id, EchelonId, DateEffet, DateFin, Indice, Version, Hash, CreatedAt) " +
            "VALUES ('IE-5-2022-03-01', '5', '2022-03-01', NULL, 180, '2022-03', 'h2', '2026-01-01T00:00:00Z');");

        Assert.Equal(150, ResolveIndiceEchelon(conn, "5", "2010-06-15"));
        Assert.Equal(150, ResolveIndiceEchelon(conn, "5", "2022-02-28"));
        Assert.Equal(180, ResolveIndiceEchelon(conn, "5", "2022-03-01"));
        Assert.Equal(180, ResolveIndiceEchelon(conn, "5", "2030-01-01"));
    }

    [Fact]
    public void Resolution_sans_plage_couverte_retourne_null()
    {
        using var scope = CreateMigratedDb();
        var conn = scope.Conn;

        // Table vide : aucune valeur applicable.
        var result = Scalar<double?>(conn,
            "SELECT Valeur FROM ValeurPoint WHERE DateEffet <= $d AND (DateFin IS NULL OR DateFin >= $d);",
            ("$d", "2026-01-01"));

        Assert.Null(result);
    }

    // -----------------------------------------------------------------------
    // Tests d'intégrité (FK / UNIQUE / CHECK)
    // -----------------------------------------------------------------------

    [Fact]
    public void FK_Grade_vers_Corps_est_enforcee()
    {
        using var scope = CreateMigratedDb();
        var conn = scope.Conn;
        var ex = Assert.Throws<SqliteException>(() =>
            Exec(conn,
                "INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, Actif, CreatedAt, Hash) " +
                "VALUES ('G-FAKE', 'Faux', 'CORPS-INEXISTANT', 1, 1, '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("FOREIGN KEY", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FK_Corps_vers_Filiere_est_enforcee()
    {
        using var scope = CreateMigratedDb();
        var conn = scope.Conn;
        var ex = Assert.Throws<SqliteException>(() =>
            Exec(conn,
                "INSERT INTO Corps (Id, Libelle, FiliereId, Actif, CreatedAt, Hash) " +
                "VALUES ('C-FAKE', 'Faux', 'FILIERE-INEXISTANTE', 1, '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("FOREIGN KEY", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FK_GrilleIndiciaire_vers_Categories_est_enforcee()
    {
        using var scope = CreateMigratedDb();
        var conn = scope.Conn;
        var ex = Assert.Throws<SqliteException>(() =>
            Exec(conn,
                "INSERT INTO GrilleIndiciaire (Id, CategorieId, DateEffet, IndiceMin, Version, Hash, CreatedAt) " +
                "VALUES ('GI-FAKE', 'CAT-INEXISTANTE', '2026-01-01', 100, 'v', 'h', '2026-01-01T00:00:00Z');"));
        Assert.Contains("FOREIGN KEY", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UNIQUE_Grade_CorpsId_Ordre_empeche_les_doublons()
    {
        using var scope = CreateMigratedDb();
        var conn = scope.Conn;

        InsertFiliere(conn, "ENSEIGNANT", "Enseignement");
        InsertCorps(conn, "PEM", "Prof. Ens. Moyen", "ENSEIGNANT");
        InsertGrade(conn, "PEM-1", "1er grade PEM", "PEM", 1);

        var ex = Assert.Throws<SqliteException>(() =>
            Exec(conn,
                "INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, Actif, CreatedAt, Hash) " +
                "VALUES ('PEM-DUP', 'Doublon', 'PEM', 1, 1, '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("UNIQUE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UNIQUE_Categorie_Niveau_pas_de_doublon_de_niveau()
    {
        using var scope = CreateMigratedDb();
        var conn = scope.Conn;

        InsertCategorie(conn, "7", 7, "7ème", horsCategorie: false);

        var ex = Assert.Throws<SqliteException>(() =>
            Exec(conn,
                "INSERT INTO Categories (Id, Niveau, Libelle, HorsCategorie, Actif, CreatedAt, Hash) " +
                "VALUES ('DUP7', 7, 'Doublon', 0, 1, '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("UNIQUE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CHECK_Categorie_Niveau_borne_1_19()
    {
        using var scope = CreateMigratedDb();
        var conn = scope.Conn;
        var ex = Assert.Throws<SqliteException>(() =>
            Exec(conn,
                "INSERT INTO Categories (Id, Niveau, Libelle, HorsCategorie, Actif, CreatedAt, Hash) " +
                "VALUES ('BADV', 25, 'Hors bornes', 0, 1, '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CHECK_Actif_accepte_uniquement_0_ou_1()
    {
        using var scope = CreateMigratedDb();
        var conn = scope.Conn;
        var ex = Assert.Throws<SqliteException>(() =>
            Exec(conn,
                "INSERT INTO Filieres (Id, Libelle, Actif, CreatedAt, Hash) " +
                "VALUES ('BAD', 'Bad', 5, '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // Helpers : inserts & queries
    // -----------------------------------------------------------------------

    private static void InsertCategorie(
        SqliteConnection c, string id, int niveau, string libelle, bool horsCategorie)
    {
        Exec(c,
            "INSERT INTO Categories (Id, Niveau, Libelle, HorsCategorie, Actif, CreatedAt, Hash) " +
            "VALUES ($id, $n, $l, $hc, 1, $t, $h);",
            ("$id", id), ("$n", niveau), ("$l", libelle),
            ("$hc", horsCategorie ? 1 : 0),
            ("$t", "2026-01-01T00:00:00Z"), ("$h", $"h-{id}"));
    }

    private static void InsertEchelon(
        SqliteConnection c, string id, int numero, string libelle)
    {
        Exec(c,
            "INSERT INTO Echelons (Id, Numero, Libelle, Actif, CreatedAt, Hash) " +
            "VALUES ($id, $n, $l, 1, $t, $h);",
            ("$id", id), ("$n", numero), ("$l", libelle),
            ("$t", "2026-01-01T00:00:00Z"), ("$h", $"h-{id}"));
    }

    private static void InsertFiliere(SqliteConnection c, string id, string libelle)
    {
        Exec(c,
            "INSERT INTO Filieres (Id, Libelle, Actif, CreatedAt, Hash) " +
            "VALUES ($id, $l, 1, $t, $h);",
            ("$id", id), ("$l", libelle),
            ("$t", "2026-01-01T00:00:00Z"), ("$h", $"h-{id}"));
    }

    private static void InsertCorps(SqliteConnection c, string id, string libelle, string filiereId)
    {
        Exec(c,
            "INSERT INTO Corps (Id, Libelle, FiliereId, Actif, CreatedAt, Hash) " +
            "VALUES ($id, $l, $f, 1, $t, $h);",
            ("$id", id), ("$l", libelle), ("$f", filiereId),
            ("$t", "2026-01-01T00:00:00Z"), ("$h", $"h-{id}"));
    }

    private static void InsertGrade(SqliteConnection c, string id, string libelle, string corpsId, int ordre)
    {
        Exec(c,
            "INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, Actif, CreatedAt, Hash) " +
            "VALUES ($id, $l, $c, $o, 1, $t, $h);",
            ("$id", id), ("$l", libelle), ("$c", corpsId), ("$o", ordre),
            ("$t", "2026-01-01T00:00:00Z"), ("$h", $"h-{id}"));
    }

    private static void InsertGrilleIndiciaire(
        SqliteConnection c, string id, string catId, string dateEffet, string? dateFin,
        int indiceMin, string version)
    {
        Exec(c,
            "INSERT INTO GrilleIndiciaire (Id, CategorieId, DateEffet, DateFin, IndiceMin, Version, Hash, CreatedAt) " +
            "VALUES ($id, $c, $de, $df, $i, $v, $h, $t);",
            ("$id", id), ("$c", catId), ("$de", dateEffet),
            ("$df", (object?)dateFin ?? DBNull.Value),
            ("$i", indiceMin), ("$v", version),
            ("$h", $"h-{id}"), ("$t", "2026-01-01T00:00:00Z"));
    }

    private static double ResolveValeurPoint(SqliteConnection c, string date) =>
        Scalar<double>(c,
            "SELECT Valeur FROM ValeurPoint " +
            "WHERE DateEffet <= $d AND (DateFin IS NULL OR DateFin >= $d) " +
            "ORDER BY DateEffet DESC LIMIT 1;",
            ("$d", date));

    private static int ResolveGrilleIndiciaire(SqliteConnection c, string catId, string date) =>
        Scalar<int>(c,
            "SELECT IndiceMin FROM GrilleIndiciaire " +
            "WHERE CategorieId = $c AND DateEffet <= $d AND (DateFin IS NULL OR DateFin >= $d) " +
            "ORDER BY DateEffet DESC LIMIT 1;",
            ("$c", catId), ("$d", date));

    private static int ResolveIndiceEchelon(SqliteConnection c, string echId, string date) =>
        Scalar<int>(c,
            "SELECT Indice FROM IndicesEchelon " +
            "WHERE EchelonId = $e AND DateEffet <= $d AND (DateFin IS NULL OR DateFin >= $d) " +
            "ORDER BY DateEffet DESC LIMIT 1;",
            ("$e", echId), ("$d", date));

    private static void Exec(SqliteConnection c, string sql, params (string Name, object? Value)[] parameters)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
        cmd.ExecuteNonQuery();
    }

    private static T Scalar<T>(SqliteConnection c, string sql, params (string Name, object? Value)[] parameters)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
        var result = cmd.ExecuteScalar();
        if (result == null || result == DBNull.Value) return default!;
        return (T)Convert.ChangeType(result, typeof(T), CultureInfo.InvariantCulture);
    }
}
