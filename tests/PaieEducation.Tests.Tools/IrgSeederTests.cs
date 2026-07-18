using Microsoft.Data.Sqlite;
using PaieEducation.Persistence.Migrations;
using PaieEducation.Seeding;

namespace PaieEducation.Tests.Tools;

/// <summary>
/// Tests d'intégration du <see cref="IrgSeeder"/> après la décision
/// <b>Q-01 du 14/07/2026</b> (révision de Q4b) : la période 2022+ pointe le
/// barème LF 2022 (6 tranches), pas le barème 2008.
/// </summary>
public class IrgSeederTests
{
    private const string ResourcePrefix = "PaieEducation.Persistence.Migrations.";

    private static (SqliteConnection conn, TempSqliteDb db) OpenMigrated()
    {
        var db = new TempSqliteDb();
        var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        var r = new SqliteMigrator(new SqliteMigratorOptions(db.ConnectionString, "test"),
            MigrationLoader.LoadFromAssembly(typeof(SqliteMigrator).Assembly, ResourcePrefix)).Apply();
        if (r.IsFailure) throw new InvalidOperationException("Migration failed: " + r.Error);
        return (conn, db);
    }

    [Fact]
    public async Task Seed_insere_les_2_baremes_2008_et_2022()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new IrgSeeder().SeedAsync(conn);

            Assert.Equal(2L, TestSupport.Scalar<long>(conn, "SELECT COUNT(*) FROM BaremeIRG;"));
            Assert.Equal(4L, TestSupport.Scalar<long>(conn,
                "SELECT COUNT(*) FROM BaremeIRGTranches WHERE BaremeId = 'IRG-2008';"));
            Assert.Equal(6L, TestSupport.Scalar<long>(conn,
                "SELECT COUNT(*) FROM BaremeIRGTranches WHERE BaremeId = 'IRG-2022';"));
        }
    }

    [Fact]
    public async Task La_periode_2022_pointe_le_bareme_2022()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new IrgSeeder().SeedAsync(conn);

            // Q-01 : « à partir de 2022-01-01 ⇒ nouveau barème » (pseudo-code).
            Assert.Equal("IRG-2022", TestSupport.Scalar<string>(conn,
                "SELECT BaremeId FROM IRGReglesPeriode WHERE Code = 'IRG-PER-2022';"));

            // Les 3 périodes antérieures restent sur le barème 2008.
            Assert.Equal(3L, TestSupport.Scalar<long>(conn,
                "SELECT COUNT(*) FROM IRGReglesPeriode WHERE BaremeId = 'IRG-2008';"));

            // Les lissages 2022+ sont conservés (fractions exactes, V007).
            Assert.Equal("137/51", TestSupport.Scalar<string>(conn,
                "SELECT CoefGeneral FROM IRGReglesPeriode WHERE Code = 'IRG-PER-2022';"));
            Assert.Equal("93/61", TestSupport.Scalar<string>(conn,
                "SELECT CoefSpecial FROM IRGReglesPeriode WHERE Code = 'IRG-PER-2022';"));
        }
    }

    [Fact]
    public async Task Bareme_2022_reproduit_l_exemple_chiffre_de_la_source()
    {
        // Cas de référence (evolution_bareme_irg_algerie_2008_2026.html, LF 2022) :
        // base imposable mensuelle 54 800 DA → tranche 3 :
        // (54 800 − 40 000) × 27 % + 4 600 = 3 996 + 4 600 = 8 596 DA.
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new IrgSeeder().SeedAsync(conn);

            Assert.Equal(8596m, IrgProgressif(conn, "IRG-2022", 54_800));
            // Contrôles de bornes : seuil d'exonération de barème (≤ 20 000 → 0).
            Assert.Equal(0m, IrgProgressif(conn, "IRG-2022", 20_000));
            // Même base sur le barème 2008 : (54 800 − 30 000) × 30 % + 4 000 = 11 440
            // — c'est l'écart qui motivait la correction Q-01 (INC-01).
            Assert.Equal(11_440m, IrgProgressif(conn, "IRG-2008", 54_800));
        }
    }

    [Fact]
    public async Task Seed_est_idempotent()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new IrgSeeder().SeedAsync(conn);
            var r2 = await new IrgSeeder().SeedAsync(conn);

            Assert.All(r2.Tables, t => Assert.Equal(0, t.Inserees));
            Assert.Equal(2L, TestSupport.Scalar<long>(conn, "SELECT COUNT(*) FROM BaremeIRG;"));
            Assert.Equal(10L, TestSupport.Scalar<long>(conn, "SELECT COUNT(*) FROM BaremeIRGTranches;"));
            Assert.Equal(4L, TestSupport.Scalar<long>(conn, "SELECT COUNT(*) FROM IRGReglesPeriode;"));
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Calcule l'IRG brut progressif mensuel à partir des tranches
    /// stockées (bornes inclusives : largeur = BorneSup − BorneInf + 1).</summary>
    private static decimal IrgProgressif(SqliteConnection c, string baremeId, int baseImposable)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT BorneInf, BorneSup, Taux FROM BaremeIRGTranches
            WHERE BaremeId = $b ORDER BY Ordre;
            """;
        cmd.Parameters.AddWithValue("$b", baremeId);

        decimal total = 0m;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var borneInf = reader.GetInt64(0);
            long? borneSup = reader.IsDBNull(1) ? null : reader.GetInt64(1);
            var taux = (decimal)reader.GetDouble(2);

            if (baseImposable < borneInf) continue;
            var plafond = borneSup is null ? baseImposable : Math.Min(baseImposable, (int)borneSup.Value);
            // Bornes inclusives : la 1re tranche démarre à 0 (largeur = plafond − 0),
            // les suivantes à BorneInf (largeur = plafond − BorneInf + 1).
            var largeur = borneInf == 0 ? plafond : plafond - borneInf + 1;
            total += largeur * taux;
        }
        return total;
    }
}
