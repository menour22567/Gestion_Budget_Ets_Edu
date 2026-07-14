using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using PaieEducation.Shared.Results;

namespace PaieEducation.Persistence.Migrations;

/// <summary>
/// Implémentation SQLite du <see cref="IMigrator"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>À chaque connexion ouverte, PRAGMA recommandés par Tome K (Persistence, §21) :
///         <list type="bullet">
///           <item><c>foreign_keys=ON</c> (SQLite perd ce réglage entre deux ouvertures)</item>
///           <item><c>journal_mode=WAL</c> (bases fichier uniquement ; ignoré sur <c>:memory:</c>)</item>
///           <item><c>busy_timeout=5000</c> (réduit les erreurs de verrouillage)</item>
///           <item><c>synchronous=NORMAL</c> (compromis performance/sécurité)</item>
///           <item><c>temp_store=MEMORY</c> (optimise les traitements temporaires)</item>
///         </list>
///   </item>
///   <item>La table méta <c>SchemaVersions</c> est garantie par bootstrap idempotent
///         (<c>CREATE TABLE IF NOT EXISTS</c>) ; elle n'est pas elle-même versionnée
///         car elle précède toute migration.</item>
///   <item>Chaque migration tourne dans sa propre transaction. L'enregistrement dans
///         <c>SchemaVersions</c> fait partie de cette transaction, garantissant que
///         seules les migrations effectivement appliquées sont tracées.</item>
///   <item><c>PRAGMA user_version</c> n'est pas utilisé (on lit/écrit dans
///         <c>SchemaVersions</c>) — cela permet d'avoir un nom, un horodatage, un
///         acteur et un checksum par migration, et de faire des requêtes SQL
///         d'audit riches.</item>
/// </list>
/// </remarks>
public sealed class SqliteMigrator : IMigrator
{
    private readonly SqliteMigratorOptions _options;
    private readonly IReadOnlyList<Migration> _migrations;

    /// <summary>
    /// Construit un migrateur. Les migrations sont triées par version croissante
    /// à la construction (pré-condition : pas de doublon, ce que <see cref="MigrationLoader"/>
    /// garantit déjà).
    /// </summary>
    public SqliteMigrator(SqliteMigratorOptions options, IEnumerable<Migration> migrations)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(migrations);

        _options = options;
        _migrations = migrations.OrderBy(m => m.Version).ToList();
    }

    /// <inheritdoc />
    public Result<int> Apply()
    {
        try
        {
            using var connection = new SqliteConnection(_options.ConnectionString);
            connection.Open();

            ConfigurePragmas(connection);
            EnsureSchemaVersionsTable(connection);

            var current = ReadMaxAppliedVersion(connection);
            var pending = _migrations.Where(m => m.Version > current).ToList();

            foreach (var migration in pending)
            {
                ApplyOne(connection, migration);
            }

            return Result.Success(pending.Count);
        }
        catch (Exception ex)
        {
            return Result.Failure<int>(Error.Failure($"Échec de la migration : {ex.Message}"));
        }
    }

    /// <inheritdoc />
    public Result<int> GetCurrentVersion()
    {
        try
        {
            using var connection = new SqliteConnection(_options.ConnectionString);
            connection.Open();
            ConfigurePragmas(connection);
            EnsureSchemaVersionsTable(connection);
            return Result.Success(ReadMaxAppliedVersion(connection));
        }
        catch (Exception ex)
        {
            return Result.Failure<int>(Error.Failure($"Lecture de la version échouée : {ex.Message}"));
        }
    }

    private void ApplyOne(SqliteConnection connection, Migration migration)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        using var tx = connection.BeginTransaction();
        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = migration.Sql;
            cmd.ExecuteNonQuery();
        }

        RecordAppliedVersion(connection, tx, migration, sw.ElapsedMilliseconds);
        tx.Commit();
    }

    private static void ConfigurePragmas(SqliteConnection connection)
    {
        // PRAGMA foreign_keys : SQLite perd ce réglage à chaque ouverture,
        // on l'active systématiquement. (V4 Tome K §21.)
        ExecPragma(connection, "PRAGMA foreign_keys=ON;");

        // PRAGMA journal_mode=WAL : ignoré sur :memory: (renvoie "memory"),
        // appliqué sur les bases fichier (meilleure concurrence).
        ExecPragma(connection, "PRAGMA journal_mode=WAL;");

        // PRAGMA busy_timeout=5000 : attend jusqu'à 5s avant de remonter
        // SQLITE_BUSY en cas de verrou.
        ExecPragma(connection, "PRAGMA busy_timeout=5000;");

        // PRAGMA synchronous=NORMAL : compromis performance/sécurité.
        // WAL + NORMAL = recommandé pour la plupart des cas (Tome K §21).
        ExecPragma(connection, "PRAGMA synchronous=NORMAL;");

        // PRAGMA temp_store=MEMORY : tables temporaires en RAM.
        ExecPragma(connection, "PRAGMA temp_store=MEMORY;");
    }

    private static void ExecPragma(SqliteConnection connection, string pragma)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = pragma;
        cmd.ExecuteNonQuery();
    }

    private static void EnsureSchemaVersionsTable(SqliteConnection connection)
    {
        // Table méta qui précède toute migration. CREATE IF NOT EXISTS la rend
        // idempotente : on peut l'appeler à chaque Apply() sans risque.
        const string ddl = """
            CREATE TABLE IF NOT EXISTS SchemaVersions (
                Version    INTEGER NOT NULL PRIMARY KEY,
                Name       TEXT    NOT NULL,
                AppliedAt  TEXT    NOT NULL,
                AppliedBy  TEXT    NOT NULL,
                DurationMs INTEGER NOT NULL,
                Checksum   TEXT    NOT NULL
            );
            """;
        using var cmd = connection.CreateCommand();
        cmd.CommandText = ddl;
        cmd.ExecuteNonQuery();
    }

    private static int ReadMaxAppliedVersion(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(Version), 0) FROM SchemaVersions;";
        var result = cmd.ExecuteScalar();
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private void RecordAppliedVersion(
        SqliteConnection connection,
        SqliteTransaction tx,
        Migration migration,
        long durationMs)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO SchemaVersions (Version, Name, AppliedAt, AppliedBy, DurationMs, Checksum)
            VALUES ($v, $n, $at, $ab, $d, $c);
            """;
        cmd.Parameters.AddWithValue("$v", migration.Version);
        cmd.Parameters.AddWithValue("$n", migration.Name);
        cmd.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$ab", _options.AppliedBy);
        cmd.Parameters.AddWithValue("$d", durationMs);
        cmd.Parameters.AddWithValue("$c", ComputeChecksum(migration.Sql));
        cmd.ExecuteNonQuery();
    }

    private static string ComputeChecksum(string sql)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sql));
        return Convert.ToHexString(bytes);
    }
}
