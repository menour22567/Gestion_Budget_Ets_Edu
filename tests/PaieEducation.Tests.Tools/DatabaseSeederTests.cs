using Microsoft.Data.Sqlite;
using PaieEducation.Persistence.Migrations;
using PaieEducation.Seeding;

namespace PaieEducation.Tests.Tools;

/// <summary>
/// Critère d'acceptation C1.1 : le seed complet s'exécute par appel
/// programmatique (via <see cref="IDataSeeder"/>/DatabaseSeeder) en
/// utilisant le CSV cascade embarqué, et peuples l'ensemble des
/// référentiels. Idempotent : un second appel ne duplique rien.
/// </summary>
public class DatabaseSeederTests
{
    private static long Count(SqliteConnection c, string table) =>
        TestSupport.Scalar<long>(c, $"SELECT COUNT(*) FROM {table};");

    [Fact]
    public async Task SeedAll_peuple_tous_les_referentiels_et_est_idempotent()
    {
        using var db = new TempSqliteDb();
        await using (var conn = new SqliteConnection(db.ConnectionString))
        {
            conn.Open();
            // Le seed suppose une base déjà migrée (comme en production).
            var migrator = new SqliteMigrator(
                new SqliteMigratorOptions(db.ConnectionString, "test"),
                MigrationLoader.LoadFromAssembly(
                    typeof(SqliteMigrator).Assembly, "PaieEducation.Persistence.Migrations."));
            Assert.True(migrator.Apply().IsSuccess);

            var seeder = new DatabaseSeeder();
            var r1 = await seeder.SeedAllAsync(conn);
            var inserees1 = r1.Tables.Sum(t => t.Inserees);

            // Second appel : idempotent (ON CONFLICT DO NOTHING).
            var r2 = await seeder.SeedAllAsync(conn);
            var inserees2 = r2.Tables.Sum(t => t.Inserees);

            Assert.True(inserees1 > 0, "Le premier seed doit insérer des lignes.");
            Assert.Equal(0, inserees2);
        }

        await using (var conn = new SqliteConnection(db.ConnectionString))
        {
            conn.Open();
            Assert.True(Count(conn, "Corps") > 0, "Corps attendus > 0");
            Assert.True(Count(conn, "Grades") >= 180L, $"Grades attendus ~185, trouvé {Count(conn, "Grades")}");
            Assert.True(Count(conn, "Rubriques") >= 10L, "Rubriques attendues >= 10");
            Assert.True(Count(conn, "BaremeIRG") == 2L, "2 barèmes IRG (2008 + 2022)");
            Assert.True(Count(conn, "IRGReglesPeriode") == 4L, "4 règles de période IRG");
            Assert.True(Count(conn, "RubriqueFormules") >= 6L, "Formules pilotes attendues");
        }
    }
}
