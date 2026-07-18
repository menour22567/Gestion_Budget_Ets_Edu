using Microsoft.Data.Sqlite;
using PaieEducation.Persistence.Migrations;
using PaieEducation.Seeding;

namespace PaieEducation.Tests.Tools;

/// <summary>
/// Tests d'intégration du <see cref="NomenclatureSeeder"/>. Chaque test
/// crée une base in-memory migrée (V001-V007), appelle le seeder, puis
/// vérifie les comptes et un échantillon de lignes.
/// </summary>
public class NomenclatureSeederTests
{
    private const string ResourcePrefix = "PaieEducation.Persistence.Migrations.";

    private static SqliteMigrator CreateMigrator(string cs) =>
        new(new SqliteMigratorOptions(cs, "test"),
            MigrationLoader.LoadFromAssembly(typeof(SqliteMigrator).Assembly, ResourcePrefix));

    private static (SqliteConnection conn, TempSqliteDb db) OpenMigrated()
    {
        var db = new TempSqliteDb();
        var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        var r = CreateMigrator(db.ConnectionString).Apply();
        if (r.IsFailure) throw new InvalidOperationException("Migration failed: " + r.Error);
        return (conn, db);
    }

    // ----- CSV d'échantillon : 5 grades, 2 filières, 1 type personnel,
    //       1 type contrat, 1 corps, 2 catégories (7, 8).
    private const string SampleCsv = """
        Num_Ord;Type_Contrat;Type_Filiere;Type_Secteur;Type_Personnel;Corps_Filiere;Grades;Categorie;"A";"B";"C";"D"
        1;Statut_Fonctionnaire;ADMIN;Education Nationale;Personnels d'Education;Corps des Adjoints de l'Education;Adjoint de l'Education;7;348;398;473;548
        2;Statut_Fonctionnaire;ADMIN;Education Nationale;Personnels d'Education;Corps des Adjoints de l'Education;Adjoint principal de l'Education;8;379;429;504;579
        3;Statut_Fonctionnaire;ENSEIGNANT;Education Nationale;Personnels Enseignants;Corps des Professeurs d'Education;Professeur d'education;12;537;587;662;737
        4;Statut_Fonctionnaire;ENSEIGNANT;Education Nationale;Personnels Enseignants;Corps des Professeurs d'Education;Professeur principal;13;578;628;703;778
        5;Statut_Fonctionnaire;ENSEIGNANT;Education Nationale;Personnels Enseignants;Corps des Professeurs d'Education;Professeur en chef;14;621;671;746;821
        """;

    private static async Task<SeedReport> RunSeed(SqliteConnection conn)
    {
        var parser = new CsvCascadeParser();
        using var sr = new StringReader(SampleCsv);
        var rows = await parser.ParseAsync(sr);
        return await new NomenclatureSeeder().SeedAsync(conn, rows);
    }

    private static long Count(SqliteConnection c, string table) =>
        TestSupport.Scalar<long>(c, $"SELECT COUNT(*) FROM {table};");

    // -------------------------------------------------------------------------
    // Filieres
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Seed_insere_les_filieres_distinctes_du_csv()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await RunSeed(conn);
            Assert.Equal(2L, Count(conn, "Filieres"));
            // ADMIN et ENSEIGNANT sont les 2 filières de l'échantillon.
            var libelles = ReadStrings(conn, "Filieres", "Libelle");
            Assert.Contains("ADMIN", libelles);
            Assert.Contains("ENSEIGNANT", libelles);
        }
    }

    // -------------------------------------------------------------------------
    // TypesContrat / TypesPersonnel
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Seed_insere_types_contrat_et_personnel()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await RunSeed(conn);
            Assert.Equal(1L, Count(conn, "TypesContrat"));
            Assert.Equal(2L, Count(conn, "TypesPersonnel"));
        }
    }

    // -------------------------------------------------------------------------
    // Echelons / Categories (codés en dur)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Seed_insere_les_12_echelons_et_19_categories()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await RunSeed(conn);
            Assert.Equal(12L, Count(conn, "Echelons"));
            Assert.Equal(19L, Count(conn, "Categories"));

            // Les niveaux 1..17 + HC-S1 (18) + HC-S2 (19).
            var niveaux = ReadLongs(conn, "Categories", "Niveau");
            for (int i = 1; i <= 17; i++) Assert.Contains((long)i, niveaux);
            Assert.Contains(18L, niveaux);
            Assert.Contains(19L, niveaux);
        }
    }

    // -------------------------------------------------------------------------
    // Corps
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Seed_insere_un_corps_par_libelle_unique_avec_FK_filiere()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await RunSeed(conn);
            Assert.Equal(2L, Count(conn, "Corps"));

            // Le corps "Corps des Adjoints de l'Education" doit pointer
            // sur la filière ADMIN.
            var join = TestSupport.Scalar<string>(conn, """
                SELECT C.Libelle || ' / ' || F.Libelle
                FROM Corps C JOIN Filieres F ON C.FiliereId = F.Id
                WHERE C.Libelle LIKE 'Corps des Adjoints%';
                """);
            Assert.Equal("Corps des Adjoints de l'Education / ADMIN", join);
        }
    }

    // -------------------------------------------------------------------------
    // Grades
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Seed_insere_5_grades_avec_FK_corps_et_ordre_unique()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await RunSeed(conn);
            Assert.Equal(5L, Count(conn, "Grades"));

            var profEnseignant = TestSupport.Scalar<long>(conn, """
                SELECT COUNT(*) FROM Grades G
                JOIN Corps C ON G.CorpsId = C.Id
                WHERE C.Libelle = 'Corps des Professeurs d''Education';
                """);
            Assert.Equal(3L, profEnseignant);
        }
    }

    // -------------------------------------------------------------------------
    // ValeurPoint
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Seed_insere_la_valeur_du_point_45_DZD_depuis_2007()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await RunSeed(conn);
            Assert.Equal(1L, Count(conn, "ValeurPoint"));

            var v = TestSupport.Scalar<double>(conn,
                "SELECT Valeur FROM ValeurPoint WHERE Id = 'VP-2007-01-01';");
            Assert.Equal(45.0, v);
        }
    }

    // -------------------------------------------------------------------------
    // GrilleIndiciaire
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Seed_insere_4_periodes_de_grille_par_categorie_presente()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await RunSeed(conn);
            // L'échantillon porte 5 catégories (7, 8, 12, 13, 14) × 4 périodes = 20.
            Assert.Equal(20L, Count(conn, "GrilleIndiciaire"));

            // La catégorie 7 doit avoir 348 à la période 2007.
            var ind7 = TestSupport.Scalar<int>(conn, """
                SELECT IndiceMin FROM GrilleIndiciaire
                WHERE CategorieId = '7' AND DateEffet = '2007-01-01';
                """);
            Assert.Equal(348, ind7);

            // Et 548 à la période 2024-01-01.
            var ind7new = TestSupport.Scalar<int>(conn, """
                SELECT IndiceMin FROM GrilleIndiciaire
                WHERE CategorieId = '7' AND DateEffet = '2024-01-01';
                """);
            Assert.Equal(548, ind7new);
        }
    }

    // -------------------------------------------------------------------------
    // Idempotence
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Seed_est_idempotent_un_deuxieme_seed_ne_duplique_pas()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            var r1 = await RunSeed(conn);
            var r2 = await RunSeed(conn);

            // Les comptes Inserees du 2e seed sont 0 partout.
            Assert.All(r2.Tables, t => Assert.Equal(0, t.Inserees));
            // Les comptes de la base n'ont pas bougé (20 = 5 cat × 4 périodes).
            Assert.Equal(2L, Count(conn, "Filieres"));
            Assert.Equal(5L, Count(conn, "Grades"));
            Assert.Equal(20L, Count(conn, "GrilleIndiciaire"));
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private static List<string> ReadStrings(SqliteConnection c, string table, string col)
    {
        var list = new List<string>();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT {col} FROM {table} ORDER BY {col};";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(reader.GetString(0));
        return list;
    }

    private static List<long> ReadLongs(SqliteConnection c, string table, string col)
    {
        var list = new List<long>();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT {col} FROM {table} ORDER BY {col};";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(reader.GetInt64(0));
        return list;
    }
}
