using Microsoft.Data.Sqlite;
using PaieEducation.Infrastructure.Time;

namespace PaieEducation.Tests.Integration;

/// <summary>
/// Tests fumée d'infrastructure : valide notamment que le provider natif SQLite
/// (SQLitePCLRaw e_sqlite3) se charge et s'exécute réellement au runtime.
/// </summary>
public class SqliteSmokeTests
{
    [Fact]
    public void Sqlite_en_memoire_execute_une_requete()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1;";

        long value = Convert.ToInt64(command.ExecuteScalar());

        Assert.Equal(1L, value);
    }

    [Fact]
    public void SystemClock_retourne_une_date_coherente()
    {
        var clock = new SystemClock();

        Assert.True(clock.UtcNow <= DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), clock.Today);
    }
}
