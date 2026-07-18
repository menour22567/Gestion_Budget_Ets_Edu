using System.Globalization;
using Microsoft.Data.Sqlite;
using PaieEducation.Persistence.Migrations;
using PaieEducation.Seeding;

namespace PaieEducation.Tools;

/// <summary>
/// CLI de seed/validate pour la base PaieEducation. Sans dépendance externe
/// (pas de System.CommandLine) : parseur d'args minimaliste écrit à la main.
/// </summary>
/// <remarks>
/// <para>Commandes :</para>
    /// <list type="bullet">
    ///   <item><c>migrate --db &lt;path&gt;</c>            : applique les migrations V001-V013</item>
    ///   <item><c>seed nomenclature --db &lt;path&gt;</c>  : nomenclature (CSV cascade embarqué)</item>
    ///   <item><c>seed reglementaire --db &lt;path&gt;</c></item>
    ///   <item><c>seed irg --db &lt;path&gt;</c></item>
    ///   <item><c>seed all --db &lt;path&gt;</c>           : tout enchaîner</item>
    ///   <item><c>validate --db &lt;path&gt;</c>           : PRAGMA integrity_check + counts</item>
    ///   <item><c>--help</c>                              : cette aide</item>
    /// </list>
/// <para>Code de retour : 0 = succès, 1 = erreur.</para>
/// </remarks>
public static class Cli
{
    private const string ResourcePrefix = "PaieEducation.Persistence.Migrations.";

    public static async Task<int> RunAsync(string[] args, TextWriter? stdout = null, TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        try
        {
            if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
            {
                PrintHelp(stdout);
                return 0;
            }

            var (verb, sub, opts) = ParseArgs(args);
            return verb switch
            {
                "migrate"        => await RunMigrateAsync(opts, stdout, stderr).ConfigureAwait(false),
                "seed" when sub == "nomenclature" => await RunSeedNomenclatureAsync(opts, stdout, stderr).ConfigureAwait(false),
                "seed" when sub == "reglementaire" => await RunSeedReglementaireAsync(opts, stdout, stderr).ConfigureAwait(false),
                "seed" when sub == "irg"          => await RunSeedIrgAsync(opts, stdout, stderr).ConfigureAwait(false),
                "seed" when sub == "all"          => await RunSeedAllAsync(opts, stdout, stderr).ConfigureAwait(false),
                "validate"        => await RunValidateAsync(opts, stdout, stderr).ConfigureAwait(false),
                _ => Fail(stderr, $"Verbe inconnu : '{verb}'. Tapez --help pour l'aide."),
            };
        }
        catch (Exception ex)
        {
            await stderr.WriteLineAsync($"ERREUR : {ex.Message}").ConfigureAwait(false);
            return 1;
        }
    }

    // -------------------------------------------------------------------------
    // Parsing
    // -------------------------------------------------------------------------
    private static (string Verb, string? Sub, Dictionary<string, string> Opts) ParseArgs(string[] args)
    {
        var verb = args[0];
        string? sub = args.Length > 1 && !args[1].StartsWith("--", StringComparison.Ordinal) ? args[1] : null;
        var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = (sub is null ? 1 : 2); i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
            var key = args[i][2..];
            var val = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i] : "true";
            opts[key] = val;
        }
        return (verb, sub, opts);
    }

    private static int Fail(TextWriter err, string msg)
    {
        err.WriteLine(msg);
        return 1;
    }

    private static void PrintHelp(TextWriter w)
    {
        w.WriteLine("""
            PaieEducation.Tools — CLI de seed/validate

            Usage:
              PaieEducation.Tools migrate --db <path>
              PaieEducation.Tools seed nomenclature --db <path>
              PaieEducation.Tools seed reglementaire --db <path>
              PaieEducation.Tools seed irg --db <path>
              PaieEducation.Tools seed all --db <path>
              PaieEducation.Tools validate --db <path>
              PaieEducation.Tools --help

            Tous les fichiers sont des chemins absolus ou relatifs au CWD.
            La base SQLite est créée si elle n'existe pas (cas : --db <nouveau>).
            Le CSV cascade de nomenclature est embarqué (aucun --csv requis).
            """);
    }

    // -------------------------------------------------------------------------
    // Migrate
    // -------------------------------------------------------------------------
    private static async Task<int> RunMigrateAsync(
        Dictionary<string, string> opts, TextWriter stdout, TextWriter stderr)
    {
        if (!opts.TryGetValue("db", out var dbPath))
            return Fail(stderr, "--db <path> requis");
        var cs = $"Data Source={dbPath}";
        var migrator = new SqliteMigrator(new SqliteMigratorOptions(cs, "tools"),
            MigrationLoader.LoadFromAssembly(typeof(SqliteMigrator).Assembly, ResourcePrefix));
        var r = migrator.Apply();
        if (r.IsFailure) return Fail(stderr, "Échec migration : " + r.Error);
        await stdout.WriteLineAsync($"✓ {r.Value} migration(s) appliquée(s) sur {dbPath}").ConfigureAwait(false);
        return 0;
    }

    // -------------------------------------------------------------------------
    // Seed : nomenclature (CSV → DB)
    // -------------------------------------------------------------------------
    private static async Task<int> RunSeedNomenclatureAsync(
        Dictionary<string, string> opts, TextWriter stdout, TextWriter stderr)
    {
        if (!opts.TryGetValue("db", out var dbPath))
            return Fail(stderr, "--db <path> requis");

        var cs = $"Data Source={dbPath}";
        await ApplyMigrationsIfNeededAsync(cs, stdout, stderr).ConfigureAwait(false);

        var rows = await SeedCsvProvider.ReadEmbeddedRowsAsync().ConfigureAwait(false);
        await stdout.WriteLineAsync($"  → {rows.Count} lignes lues dans le CSV embarqué").ConfigureAwait(false);

        await using var conn = new SqliteConnection(cs);
        conn.Open();
        var report = await new NomenclatureSeeder().SeedAsync(conn, rows).ConfigureAwait(false);
        PrintReport(stdout, report, "nomenclature");
        return 0;
    }

    // -------------------------------------------------------------------------
    // Seed : réglementaire
    // -------------------------------------------------------------------------
    private static async Task<int> RunSeedReglementaireAsync(
        Dictionary<string, string> opts, TextWriter stdout, TextWriter stderr)
    {
        if (!opts.TryGetValue("db", out var dbPath))
            return Fail(stderr, "--db <path> requis");

        var cs = $"Data Source={dbPath}";
        await ApplyMigrationsIfNeededAsync(cs, stdout, stderr).ConfigureAwait(false);

        await using var conn = new SqliteConnection(cs);
        conn.Open();
        var report = await new ReglementaireSeeder().SeedAsync(conn).ConfigureAwait(false);
        PrintReport(stdout, report, "réglementaire");
        return 0;
    }

    // -------------------------------------------------------------------------
    // Seed : IRG
    // -------------------------------------------------------------------------
    private static async Task<int> RunSeedIrgAsync(
        Dictionary<string, string> opts, TextWriter stdout, TextWriter stderr)
    {
        if (!opts.TryGetValue("db", out var dbPath))
            return Fail(stderr, "--db <path> requis");

        var cs = $"Data Source={dbPath}";
        await ApplyMigrationsIfNeededAsync(cs, stdout, stderr).ConfigureAwait(false);

        await using var conn = new SqliteConnection(cs);
        conn.Open();
        var report = await new IrgSeeder().SeedAsync(conn).ConfigureAwait(false);
        PrintReport(stdout, report, "IRG");
        return 0;
    }

    // -------------------------------------------------------------------------
    // Seed : all (migrate + nomenclature + réglementaire + irg)
    // -------------------------------------------------------------------------
    private static async Task<int> RunSeedAllAsync(
        Dictionary<string, string> opts, TextWriter stdout, TextWriter stderr)
    {
        if (!opts.TryGetValue("db", out var dbPath))
            return Fail(stderr, "--db <path> requis");

        var cs = $"Data Source={dbPath}";

        // 1. Migrations.
        await ApplyMigrationsIfNeededAsync(cs, stdout, stderr).ConfigureAwait(false);

        await using var conn = new SqliteConnection(cs);
        conn.Open();

        // 2. Seed complet (nomenclature embarquée + réglementaire + IRG + formules).
        var report = await new DatabaseSeeder().SeedAllAsync(conn).ConfigureAwait(false);

        // 3. Rapport agrégé.
        PrintReport(stdout, report, "all");
        return 0;
    }

    // -------------------------------------------------------------------------
    // Validate : PRAGMA integrity_check + counts
    // -------------------------------------------------------------------------
    private static async Task<int> RunValidateAsync(
        Dictionary<string, string> opts, TextWriter stdout, TextWriter stderr)
    {
        if (!opts.TryGetValue("db", out var dbPath))
            return Fail(stderr, "--db <path> requis");
        if (!File.Exists(dbPath))
            return Fail(stderr, $"Base introuvable : {dbPath}");

        var cs = $"Data Source={dbPath}";
        await using var conn = new SqliteConnection(cs);
        conn.Open();

        var integ = (string?)await ScalarAsync<string>(conn, "PRAGMA integrity_check;")!;
        var fkCheck = await QueryAsync<string>(conn, "PRAGMA foreign_key_check;");

        await stdout.WriteLineAsync($"PRAGMA integrity_check : {integ}").ConfigureAwait(false);
        if (fkCheck.Count > 0)
        {
            await stderr.WriteLineAsync($"PRAGMA foreign_key_check : {fkCheck.Count} violation(s)").ConfigureAwait(false);
            foreach (var row in fkCheck)
                await stderr.WriteLineAsync($"  {row}").ConfigureAwait(false);
        }
        else
        {
            await stdout.WriteLineAsync("PRAGMA foreign_key_check : OK").ConfigureAwait(false);
        }

        // Counts sur les tables métier principales.
        var tables = new[] {
            "SchemaVersions", "Filieres", "TypesContrat", "TypesPersonnel",
            "Echelons", "Categories", "Corps", "Grades",
            "ValeurPoint", "GrilleIndiciaire", "Rubriques", "Cotisations",
            "BaremeIRG", "BaremeIRGTranches", "IRGReglesPeriode", "Parametres",
        };
        await stdout.WriteLineAsync("Counts :").ConfigureAwait(false);
        foreach (var t in tables)
        {
            var nObj = await ScalarAsync<long>(conn, $"SELECT COUNT(*) FROM {t};");
            long n = nObj is null or DBNull ? 0L : Convert.ToInt64(nObj, CultureInfo.InvariantCulture);
            await stdout.WriteLineAsync($"  {t,-25} {n,6}").ConfigureAwait(false);
        }

        var ok = integ == "ok" && fkCheck.Count == 0;
        await stdout.WriteLineAsync(ok ? "✓ Base OK" : "✗ Base INVALIDE").ConfigureAwait(false);
        return ok ? 0 : 1;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private static async Task ApplyMigrationsIfNeededAsync(
        string cs, TextWriter stdout, TextWriter stderr)
    {
        var migrator = new SqliteMigrator(new SqliteMigratorOptions(cs, "tools"),
            MigrationLoader.LoadFromAssembly(typeof(SqliteMigrator).Assembly, ResourcePrefix));
        var r = migrator.Apply();
        if (r.IsFailure) throw new InvalidOperationException("Échec migration : " + r.Error);
        if (r.Value > 0)
            await stdout.WriteLineAsync($"  → {r.Value} migration(s) appliquée(s)").ConfigureAwait(false);
    }

    private static void PrintReport(TextWriter stdout, SeedReport r, string label)
    {
        stdout.WriteLine($"  Seed {label} :");
        foreach (var t in r.Tables)
        {
            stdout.WriteLine($"    {t.Table,-25} lues={t.Lues,4}  inserees={t.Inserees,4}");
        }
    }

    private static async Task<object?> ScalarAsync<T>(SqliteConnection c, string sql)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return await cmd.ExecuteScalarAsync().ConfigureAwait(false);
    }

    private static async Task<List<string>> QueryAsync<T>(SqliteConnection c, string sql)
    {
        var list = new List<string>();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            for (int i = 0; i < reader.FieldCount; i++)
                list.Add(reader.IsDBNull(i) ? "NULL" : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? "NULL");
        }
        return list;
    }
}
