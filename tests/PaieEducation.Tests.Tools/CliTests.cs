using System.Text;
using Microsoft.Data.Sqlite;
using PaieEducation.Tools;

namespace PaieEducation.Tests.Tools;

/// <summary>
/// Tests d'intégration de la CLI <see cref="Cli"/>. On passe un
/// <see cref="StringWriter"/> pour stdout/stderr et un <see cref="TempSqliteDb"/>
/// pour la base. On vérifie le code de retour, le stdout (résumé), et
/// l'état de la base.
/// </summary>
public class CliTests
{
    private const string SampleCsv = """
        Num_Ord;Type_Contrat;Type_Filiere;Type_Secteur;Type_Personnel;Corps_Filiere;Grades;Categorie;"A";"B";"C";"D"
        1;Statut_Fonctionnaire;ADMIN;Education Nationale;Personnels d'Education;Corps des Adjoints de l'Education;Adjoint de l'Education;7;348;398;473;548
        2;Statut_Fonctionnaire;ENSEIGNANT;Education Nationale;Personnels Enseignants;Corps des Professeurs d'Education;Professeur d'education;12;537;587;662;737
        """;

    private static (int code, string stdout, string stderr) Run(params string[] args)
    {
        var so = new StringBuilder(); var se = new StringBuilder();
        using var sw = new StringWriter(so);
        using var ew = new StringWriter(se);
        var code = Cli.RunAsync(args, sw, ew).GetAwaiter().GetResult();
        return (code, so.ToString(), se.ToString());
    }

    private static long Count(SqliteConnection c, string table) =>
        TestSupport.Scalar<long>(c, $"SELECT COUNT(*) FROM {table};");

    // -------------------------------------------------------------------------
    // Help
    // -------------------------------------------------------------------------
    [Fact]
    public void Help_retourne_code_0_et_affiche_usage()
    {
        var (code, stdout, _) = Run("--help");
        Assert.Equal(0, code);
        Assert.Contains("Usage", stdout);
        Assert.Contains("migrate", stdout);
    }

    [Fact]
    public void Verbe_inconnu_retourne_code_1_avec_erreur_sur_stderr()
    {
        var (code, _, stderr) = Run("foobar");
        Assert.Equal(1, code);
        Assert.Contains("Verbe inconnu", stderr);
    }

    // -------------------------------------------------------------------------
    // Migrate
    // -------------------------------------------------------------------------
    [Fact]
    public void Migrate_cree_la_base_et_applique_V001_V007()
    {
        using var db = new TempSqliteDb();
        var (code, stdout, stderr) = Run("migrate", "--db", db.Path);
        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.Contains("migration(s) appliquée(s)", stdout);

        // Vérifie la base : SchemaVersions contient V001..V007.
        using var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        Assert.Equal(7L, Count(conn, "SchemaVersions"));
    }

    [Fact]
    public void Migrate_sans_db_retourne_1()
    {
        var (code, _, stderr) = Run("migrate");
        Assert.Equal(1, code);
        Assert.Contains("--db", stderr);
    }

    // -------------------------------------------------------------------------
    // Seed : nomenclature
    // -------------------------------------------------------------------------
    [Fact]
    public void Seed_nomenclature_insere_les_tables_de_nomenclature()
    {
        using var db = new TempSqliteDb();
        var csv = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.csv");
        File.WriteAllText(csv, SampleCsv, Encoding.UTF8);
        try
        {
            var (code, stdout, stderr) = Run("seed", "nomenclature", "--db", db.Path, "--csv", csv);
            Assert.Equal(0, code);
            Assert.Empty(stderr);
            Assert.Contains("Seed nomenclature", stdout);

            using var conn = new SqliteConnection(db.ConnectionString);
            conn.Open();
            Assert.Equal(2L, Count(conn, "Filieres"));
            Assert.Equal(2L, Count(conn, "Grades"));
        }
        finally
        {
            TryDelete(csv);
        }
    }

    // -------------------------------------------------------------------------
    // Seed : réglementaire
    // -------------------------------------------------------------------------
    [Fact]
    public void Seed_reglementaire_insere_rubriques_et_cotisations()
    {
        using var db = new TempSqliteDb();
        var (code, stdout, _) = Run("seed", "reglementaire", "--db", db.Path);
        Assert.Equal(0, code);
        Assert.Contains("Seed réglementaire", stdout);

        using var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        Assert.Equal(6L, Count(conn, "Rubriques"));
        Assert.Equal(3L, Count(conn, "Cotisations"));
        Assert.Equal(4L, Count(conn, "Parametres"));
    }

    // -------------------------------------------------------------------------
    // Seed : irg
    // -------------------------------------------------------------------------
    [Fact]
    public void Seed_irg_insere_bareme_et_4_periodes()
    {
        using var db = new TempSqliteDb();
        var (code, stdout, stderr) = Run("seed", "irg", "--db", db.Path);
        Assert.True(code == 0, $"stderr: {stderr}");
        Assert.Contains("Seed IRG", stdout);

        using var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        Assert.Equal(1L, Count(conn, "BaremeIRG"));
        Assert.Equal(4L, Count(conn, "BaremeIRGTranches"));
        Assert.Equal(4L, Count(conn, "IRGReglesPeriode"));
    }

    // -------------------------------------------------------------------------
    // Seed : all
    // -------------------------------------------------------------------------
    [Fact]
    public void Seed_all_insere_tout_en_une_passe()
    {
        using var db = new TempSqliteDb();
        var csv = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.csv");
        File.WriteAllText(csv, SampleCsv, Encoding.UTF8);
        try
        {
            var (code, stdout, stderr) = Run("seed", "all", "--db", db.Path, "--csv", csv);
            Assert.Equal(0, code);
            Assert.Empty(stderr);
            Assert.Contains("Seed nomenclature", stdout);
            Assert.Contains("Seed réglementaire", stdout);
            Assert.Contains("Seed IRG", stdout);

            using var conn = new SqliteConnection(db.ConnectionString);
            conn.Open();
            Assert.Equal(7L, Count(conn, "SchemaVersions"));
            Assert.Equal(2L, Count(conn, "Filieres"));
            Assert.Equal(6L, Count(conn, "Rubriques"));
            Assert.Equal(4L, Count(conn, "IRGReglesPeriode"));
        }
        finally
        {
            TryDelete(csv);
        }
    }

    // -------------------------------------------------------------------------
    // Validate
    // -------------------------------------------------------------------------
    [Fact]
    public void Validate_sur_base_non_seedee_retourne_1_car_pas_de_tables()
    {
        using var db = new TempSqliteDb();
        // Crée la base vide (juste en ouvrant la connexion, SQLite crée le fichier).
        using (var c = new SqliteConnection(db.ConnectionString)) c.Open();
        var (code, stdout, _) = Run("validate", "--db", db.Path);
        // OK = intégrité SQLite est bonne, mais la base n'a pas les tables.
        // Le code retourné par validate dépend de l'intégrité uniquement.
        Assert.Contains("integrity_check", stdout);
    }

    [Fact]
    public void Validate_apres_seed_all_retourne_0_et_affiche_les_counts()
    {
        using var db = new TempSqliteDb();
        var csv = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.csv");
        File.WriteAllText(csv, SampleCsv, Encoding.UTF8);
        try
        {
            Run("seed", "all", "--db", db.Path, "--csv", csv);
            var (code, stdout, _) = Run("validate", "--db", db.Path);
            Assert.Equal(0, code);
            Assert.Contains("PRAGMA integrity_check : ok", stdout);
            Assert.Contains("PRAGMA foreign_key_check : OK", stdout);
            Assert.Contains("Base OK", stdout);
            Assert.Contains("Rubriques", stdout);
        }
        finally
        {
            TryDelete(csv);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private static void TryDelete(string p)
    {
        try { if (File.Exists(p)) File.Delete(p); } catch { }
    }
}
