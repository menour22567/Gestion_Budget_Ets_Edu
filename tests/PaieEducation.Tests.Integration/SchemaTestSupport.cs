using System.Globalization;
using Microsoft.Data.Sqlite;
using PaieEducation.Persistence.Migrations;

namespace PaieEducation.Tests.Integration;

/// <summary>
/// Helpers partagés par les tests d'intégration du schéma :
///  - <see cref="SchemaTestScope"/> encapsule (db temp, connexion migrée) et
///    les dispose dans le bon ordre.
///  - <see cref="SchemaTestSupport"/> regroupe l'outillage de bas niveau
///    (Exec / Scalar / Row) pour ne pas le dupliquer dans chaque fichier.
/// </summary>
internal static class SchemaTestSupport
{
    public const string ResourcePrefix = "PaieEducation.Persistence.Migrations.";

    public static SchemaTestScope CreateMigrated()
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
        return new SchemaTestScope(db, conn);
    }

    public static void Exec(SqliteConnection c, string sql, params (string Name, object? Value)[] parameters)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
        cmd.ExecuteNonQuery();
    }

    public static T Scalar<T>(SqliteConnection c, string sql, params (string Name, object? Value)[] parameters)
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

internal sealed record SchemaTestScope(TempSqliteDb Db, SqliteConnection Conn) : IDisposable
{
    public void Dispose()
    {
        Conn?.Dispose();
        Db?.Dispose();
    }
}
