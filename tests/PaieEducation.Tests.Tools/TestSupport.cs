using System.Globalization;
using Microsoft.Data.Sqlite;

namespace PaieEducation.Tests.Tools;

/// <summary>
/// Helpers de bas niveau pour les tests d'outils : base SQLite temporaire,
/// Exec/Scalar/Count. Copie locale (légère) des helpers du projet
/// <c>PaieEducation.Tests.Integration</c> pour éviter une dépendance
/// inter-projet de tests.
/// </summary>
internal static class TestSupport
{
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
        if (result is null or DBNull) return default!;
        return (T)Convert.ChangeType(result, typeof(T), CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Base SQLite temporaire sur disque. On utilise un fichier (pas :memory:)
/// pour observer les changements faits par le migrateur (qui ouvre ses
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
            $"paie-edu-tools-test-{Guid.NewGuid():N}.db");
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
