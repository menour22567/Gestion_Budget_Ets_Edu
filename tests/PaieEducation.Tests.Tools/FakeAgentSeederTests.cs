using Microsoft.Data.Sqlite;
using PaieEducation.Persistence.Migrations;
using PaieEducation.Seeding;

namespace PaieEducation.Tests.Tools;

/// <summary>
/// Garde de non-régression du <see cref="FakeAgentSeeder"/> (fixture de test).
/// Contre le seed canonique, les 29 agents s'insèrent sans violation de clé
/// étrangère et le seeder est idempotent : une seconde passe n'insère rien et
/// ne crée aucune carrière orpheline.
/// </summary>
/// <remarks>
/// Verrouille le correctif du 21/07/2026 : le seeder mintait un nouvel AgentId
/// à chaque passe alors que l'agent était ignoré sur conflit de matricule, ce
/// qui laissait la carrière référencer un AgentId inexistant → <c>FOREIGN KEY
/// constraint failed</c> à la seconde passe (le cas exercé par
/// <see cref="DatabaseSeederTests"/> via l'idempotence de <c>SeedAll</c>).
/// </remarks>
public class FakeAgentSeederTests
{
    private static long Count(SqliteConnection c, string table) =>
        TestSupport.Scalar<long>(c, $"SELECT COUNT(*) FROM {table};");

    [Fact]
    public async Task FakeAgents_s_inserent_sans_FK_et_sont_idempotents()
    {
        using var db = new TempSqliteDb();
        await using var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        // FK activées comme en production (App.xaml.cs) pour attraper tout orphelin.
        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys=ON;";
            pragma.ExecuteNonQuery();
        }

        var migrator = new SqliteMigrator(
            new SqliteMigratorOptions(db.ConnectionString, "test"),
            MigrationLoader.LoadFromAssembly(
                typeof(SqliteMigrator).Assembly, "PaieEducation.Persistence.Migrations."));
        Assert.True(migrator.Apply().IsSuccess);

        // Référentiels canoniques, sans les agents fictifs.
        await new DatabaseSeeder { SeedFakeAgents = false }.SeedAllAsync(conn);

        var seeder = new FakeAgentSeeder();
        var passe1 = await seeder.SeedAsync(conn);
        var passe2 = await seeder.SeedAsync(conn); // idempotence : ne doit rien réinsérer

        Assert.Equal(29, passe1.Tables.Sum(t => t.Inserees));
        Assert.Equal(0, passe2.Tables.Sum(t => t.Inserees));
        Assert.Equal(29L, Count(conn, "Agents"));
        Assert.Equal(29L, Count(conn, "Carrieres"));
        // Aucune carrière orpheline (la FK aurait déjà levé — ceinture et bretelles).
        Assert.Equal(0L, TestSupport.Scalar<long>(conn,
            "SELECT COUNT(*) FROM Carrieres c LEFT JOIN Agents a ON a.Id = c.AgentId WHERE a.Id IS NULL;"));
    }
}
