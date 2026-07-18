using Microsoft.Data.Sqlite;
using PaieEducation.Persistence.Migrations;
using PaieEducation.Seeding;

namespace PaieEducation.Tests.Tools;

/// <summary>
/// Critère d'acceptation C1.3 : l'agent de démonstration (A-PILOTE) est
/// insérable de façon idempotente, et dépend du socle nomenclature.
/// </summary>
public class DemoAgentSeederTests
{
    private static long Count(SqliteConnection c, string table) =>
        TestSupport.Scalar<long>(c, $"SELECT COUNT(*) FROM {table};");

    private static void Migrer(SqliteConnection c)
    {
        var migrator = new SqliteMigrator(
            new SqliteMigratorOptions(c.ConnectionString, "test"),
            MigrationLoader.LoadFromAssembly(
                typeof(SqliteMigrator).Assembly, "PaieEducation.Persistence.Migrations."));
        Assert.True(migrator.Apply().IsSuccess);
    }

    [Fact]
    public async Task Seed_insere_agent_demo_et_est_idempotent()
    {
        using var db = new TempSqliteDb();
        await using (var conn = new SqliteConnection(db.ConnectionString))
        {
            conn.Open();
            Migrer(conn);

            var seeder = new DemoAgentSeeder();
            var r1 = await seeder.SeedAsync(conn);
            var inserees1 = r1.Tables.Sum(t => t.Inserees);
            var r2 = await seeder.SeedAsync(conn);
            var inserees2 = r2.Tables.Sum(t => t.Inserees);

            // 1 agent + 1 carrière insérés au 1er passage.
            Assert.Equal(2, inserees1);
            Assert.Equal(0, inserees2);
        }

        await using (var conn = new SqliteConnection(db.ConnectionString))
        {
            conn.Open();
            Assert.Equal(1L, Count(conn, "Agents"));
            Assert.Equal("A-PILOTE", TestSupport.Scalar<string>(conn, "SELECT Id FROM Agents;"));
        }
    }
}
