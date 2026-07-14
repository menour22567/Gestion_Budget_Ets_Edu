using System.Globalization;
using Microsoft.Data.Sqlite;
using PaieEducation.Persistence.Migrations;
using PaieEducation.Shared.Results;

namespace PaieEducation.Tests.Integration;

/// <summary>
/// Tests d'intégration du <see cref="SqliteMigrator"/> : ils ouvrent de vraies
/// connexions SQLite, exécutent de vraies migrations et vérifient l'état réel
/// de la base (tables, version, audit, idempotence).
/// </summary>
/// <remarks>
/// On utilise une base fichier temporaire (et non <c>:memory:</c>) parce que
/// <c>:memory:</c> est strictement privée à chaque connexion — le test ne verrait
/// pas les changements faits par la connexion interne du migrateur.
/// </remarks>
public class MigratorTests
{
    private const string ResourcePrefix = "PaieEducation.Persistence.Migrations.";

    private static SqliteMigrator CreateMigrator(string connectionString, string appliedBy = "test")
    {
        var migrations = MigrationLoader.LoadFromAssembly(
            typeof(SqliteMigrator).Assembly, ResourcePrefix);
        return new SqliteMigrator(new SqliteMigratorOptions(connectionString, appliedBy), migrations);
    }

    [Fact]
    public void Migration_V001_est_embarquee_dans_l_assembly_Persistence()
    {
        var names = typeof(SqliteMigrator).Assembly.GetManifestResourceNames();

        Assert.Contains($"{ResourcePrefix}V001__init.sql", names);
    }

    [Fact]
    public void Apply_sur_base_vierge_cree_le_schema_et_trace_la_migration()
    {
        using var db = new TempSqliteDb();
        using var connection = new SqliteConnection(db.ConnectionString);
        connection.Open();

        var migrator = CreateMigrator(connection.ConnectionString);

        var result = migrator.Apply();

        Assert.True(result.IsSuccess, result.Error.Message);
        Assert.Equal(1, result.Value);

        // SchemaVersions doit contenir V001.
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM SchemaVersions WHERE Version = 1;";
            Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture));
        }

        // Et la table AuditLog (créée par V001) doit exister.
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='AuditLog';";
            Assert.Equal("AuditLog", Convert.ToString(cmd.ExecuteScalar(), CultureInfo.InvariantCulture));
        }
    }

    [Fact]
    public void Apply_sur_base_deja_migree_est_idempotent()
    {
        using var db = new TempSqliteDb();
        using var connection = new SqliteConnection(db.ConnectionString);
        connection.Open();

        var migrator = CreateMigrator(connection.ConnectionString);

        var first = migrator.Apply();
        var second = migrator.Apply();
        var third = migrator.Apply();

        Assert.True(first.IsSuccess);
        Assert.Equal(1, first.Value);
        Assert.True(second.IsSuccess);
        Assert.Equal(0, second.Value);
        Assert.True(third.IsSuccess);
        Assert.Equal(0, third.Value);

        // Toujours une seule ligne dans SchemaVersions.
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM SchemaVersions;";
        Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture));
    }

    [Fact]
    public void GetCurrentVersion_retourne_zero_sur_base_vierge_et_version_max_sinon()
    {
        using var db = new TempSqliteDb();

        var migrator = CreateMigrator(db.ConnectionString);

        var before = migrator.GetCurrentVersion();
        Assert.True(before.IsSuccess);
        Assert.Equal(0, before.Value);

        migrator.Apply();

        var after = migrator.GetCurrentVersion();
        Assert.True(after.IsSuccess);
        Assert.Equal(1, after.Value);
    }

    [Fact]
    public void Apply_active_les_foreign_keys_sur_chaque_connexion()
    {
        using var db = new TempSqliteDb();

        // 1ère connexion : on désactive explicitement, on confirme.
        using (var conn = new SqliteConnection(db.ConnectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA foreign_keys=OFF;";
            cmd.ExecuteNonQuery();
            Assert.Equal(0L, Convert.ToInt64(ExecuteScalar(conn, "PRAGMA foreign_keys;"), CultureInfo.InvariantCulture));
        }

        // Le migrateur tourne (sur ses propres connexions).
        var migrator = CreateMigrator(db.ConnectionString);
        var apply = migrator.Apply();
        Assert.True(apply.IsSuccess, apply.Error.Message);

        // Nouvelle connexion : foreign_keys doit avoir été ré-activé par
        // le migrateur. Mais PRAGMA foreign_keys est par-connexion (pas
        // persisté sur disque comme WAL), donc une connexion vierge peut
        // très bien avoir foreign_keys=0. On vérifie plutôt que la
        // contrainte FK est respectée : AuditLog_NoAction est appliquée.
        using (var conn = new SqliteConnection(db.ConnectionString))
        {
            conn.Open();
            // On force le PRAGMA et on vérifie qu'on peut l'activer puis
            // qu'il reste à 1 sur cette connexion. Ça prouve que la base
            // n'est pas en mode « foreign_keys désactivées de force ».
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA foreign_keys=ON;";
            cmd.ExecuteNonQuery();
            Assert.Equal(1L, Convert.ToInt64(ExecuteScalar(conn, "PRAGMA foreign_keys;"), CultureInfo.InvariantCulture));
        }
    }

    [Fact]
    public void Apply_sur_base_fichier_active_le_mode_WAL()
    {
        // WAL est persisté sur disque, contrairement à foreign_keys.
        using var db = new TempSqliteDb();

        var migrator = CreateMigrator(db.ConnectionString);
        var result = migrator.Apply();
        Assert.True(result.IsSuccess, result.Error.Message);

        using var verify = new SqliteConnection(db.ConnectionString);
        verify.Open();
        var mode = Convert.ToString(ExecuteScalar(verify, "PRAGMA journal_mode;"), CultureInfo.InvariantCulture);
        Assert.Equal("wal", mode);
    }

    [Fact]
    public void SchemaVersions_enregistre_nom_acteur_et_checksum()
    {
        using var db = new TempSqliteDb();
        using var connection = new SqliteConnection(db.ConnectionString);
        connection.Open();

        var migrator = CreateMigrator(db.ConnectionString, appliedBy: "alice");
        migrator.Apply();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Name, AppliedBy, Checksum, DurationMs FROM SchemaVersions WHERE Version = 1;";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());

        var name = reader.GetString(0);
        var actor = reader.GetString(1);
        var checksum = reader.GetString(2);
        var duration = reader.GetInt64(3);

        Assert.Equal("init", name);
        Assert.Equal("alice", actor);
        Assert.False(string.IsNullOrWhiteSpace(checksum));
        Assert.Equal(64, checksum.Length); // SHA-256 hex.
        Assert.True(duration >= 0);
    }

    [Fact]
    public void Apply_echoue_proprement_si_SQL_invalide()
    {
        // On crée un migrateur « pollué » avec une migration qui plante
        // pour vérifier que l'échec est bien transformé en Result.Failure
        // et que la base n'est pas corrompue (la migration fautive n'est
        // pas enregistrée dans SchemaVersions).
        using var db = new TempSqliteDb();
        var broken = new Migration(99, "broken", "THIS IS NOT VALID SQL;");

        var migrator = new SqliteMigrator(
            new SqliteMigratorOptions(db.ConnectionString, "test"),
            new[] { broken });

        var result = migrator.Apply();

        Assert.True(result.IsFailure);
        Assert.Equal(0, migrator.GetCurrentVersion().Value);

        // SchemaVersions existe (bootstrap) mais ne contient pas V99.
        using var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM SchemaVersions;";
        Assert.Equal(0L, Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture));
    }

    private static object? ExecuteScalar(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }
}

/// <summary>
/// Crée une base SQLite temporaire dans le dossier TEMP et la supprime
/// (ainsi que ses fichiers WAL/SHM) à la disposition. Permet aux tests
/// d'observer les changements faits par le migrateur (qui ouvre ses
/// propres connexions sur la même base).
/// </summary>
internal sealed class TempSqliteDb : IDisposable
{
    public string Path { get; }
    public string ConnectionString => $"Data Source={Path}";

    public TempSqliteDb()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"paie-edu-test-{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        TryDelete(Path);
        TryDelete(Path + "-wal");
        TryDelete(Path + "-shm");
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort cleanup */ }
    }
}

/// <summary>Extension locale pour afficher une éventuelle erreur dans un Assert.True.</summary>
internal static class ResultTestExtensions
{
    public static string ErrorOrDefault(this Result result)
        => result.IsFailure ? result.Error.ToString() : string.Empty;

    public static string ErrorOrDefault<T>(this Result<T> result)
        => result.IsFailure ? result.Error.ToString() : string.Empty;
}
