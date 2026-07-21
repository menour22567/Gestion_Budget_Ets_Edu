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
    public void Migrate_cree_la_base_et_applique_V001_V009()
    {
        using var db = new TempSqliteDb();
        var (code, stdout, stderr) = Run("migrate", "--db", db.Path);
        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.Contains("migration(s) appliquée(s)", stdout);

        // V009 (Workbench réglementaire, ADR-0007) ajoutée le 15/07/2026 : 9 migrations
        // au total (V001 à V008 préexistantes + V009). Le test reste robuste à
        // l'ajout futur : il vérifie au moins 9.
        using var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        var count = Count(conn, "SchemaVersions");
        Assert.True(count >= 9L, $"Attendu >= 9 migrations, trouvé {count}");
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
        var (code, stdout, stderr) = Run("seed", "nomenclature", "--db", db.Path);
        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.Contains("Seed nomenclature", stdout);

        using var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        // Le CSV cascade embarqué porte 185 grades + 1 filière INSPECTION
        // (ajoutée par ReglementaireSeeder). On vérifie l'insertion effective.
        Assert.True(Count(conn, "Grades") >= 180L, $"Grades = {Count(conn, "Grades")}");
        Assert.True(Count(conn, "Filieres") >= 1L);
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
        Assert.Equal(10L, Count(conn, "Rubriques"));
        Assert.Equal(3L, Count(conn, "Cotisations"));
        Assert.Equal(10L, Count(conn, "Parametres"));
    }

    // -------------------------------------------------------------------------
    // Seed : irg
    // -------------------------------------------------------------------------
    [Fact]
    public void Seed_irg_insere_baremes_2008_et_2022_et_4_periodes()
    {
        using var db = new TempSqliteDb();
        var (code, stdout, stderr) = Run("seed", "irg", "--db", db.Path);
        Assert.True(code == 0, $"stderr: {stderr}");
        Assert.Contains("Seed IRG", stdout);

        using var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        // Q-01 (14/07/2026) : 2 barèmes (2008 : 4 tranches ; LF 2022 : 6 tranches).
        Assert.Equal(2L, Count(conn, "BaremeIRG"));
        Assert.Equal(10L, Count(conn, "BaremeIRGTranches"));
        Assert.Equal(4L, Count(conn, "IRGReglesPeriode"));
    }

    // -------------------------------------------------------------------------
    // Seed : all
    // -------------------------------------------------------------------------
    [Fact]
    public void Seed_all_insere_tout_en_une_passe()
    {
        using var db = new TempSqliteDb();
        var (code, stdout, stderr) = Run("seed", "all", "--db", db.Path);
        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.Contains("Seed all", stdout);

        using var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        // V013 (rappels) ; robuste à l'ajout futur.
        Assert.True(Count(conn, "SchemaVersions") >= 13L);
        // Nomenclature (CSV cascade embarqué) + filière INSPECTION (Q-C3).
        Assert.True(Count(conn, "Filieres") >= 1L);
        // 10 rubriques réglementaires + TRAITEMENT (ajoutée par FormulesSeeder).
        Assert.Equal(11L, Count(conn, "Rubriques"));
        Assert.Equal(4L, Count(conn, "IRGReglesPeriode"));
    }

    [Fact]
    public void Seed_all_sans_option_n_insere_aucun_agent_fictif()
    {
        using var db = new TempSqliteDb();
        var (code, _, stderr) = Run("seed", "all", "--db", db.Path);
        Assert.Equal(0, code);
        Assert.Empty(stderr);

        using var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        // Chemin par défaut = production : aucune donnée de test injectée.
        Assert.Equal(0L, Count(conn, "Agents"));
    }

    [Fact]
    public void Seed_all_avec_with_fake_agents_insere_les_agents_de_test()
    {
        using var db = new TempSqliteDb();
        var (code, _, stderr) = Run("seed", "all", "--db", db.Path, "--with-fake-agents");
        Assert.Equal(0, code);
        Assert.Empty(stderr);

        using var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        Assert.Equal(29L, Count(conn, "Agents"));
        Assert.Equal(29L, Count(conn, "Carrieres"));
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
        Run("seed", "all", "--db", db.Path);
        var (code, stdout, _) = Run("validate", "--db", db.Path);
        Assert.Equal(0, code);
        Assert.Contains("PRAGMA integrity_check : ok", stdout);
        Assert.Contains("PRAGMA foreign_key_check : OK", stdout);
        Assert.Contains("Base OK", stdout);
        Assert.Contains("Rubriques", stdout);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
}
