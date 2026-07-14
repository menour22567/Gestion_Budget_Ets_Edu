using Microsoft.Data.Sqlite;
using PaieEducation.Persistence.Migrations;
using PaieEducation.Tools.Seeding;

namespace PaieEducation.Tests.Tools;

/// <summary>
/// Tests d'intégration du <see cref="ReglementaireSeeder"/>. Chaque test
/// crée une base migrée et vérifie le seed réglementaire (rubriques,
/// règles d'éligibilité, cotisations, paramètres).
/// </summary>
public class ReglementaireSeederTests
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

    private static long Count(SqliteConnection c, string table) =>
        TestSupport.Scalar<long>(c, $"SELECT COUNT(*) FROM {table};");

    // -------------------------------------------------------------------------
    // Rubriques
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Seed_insere_les_6_rubriques_canoniques()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            var report = await new ReglementaireSeeder().SeedAsync(conn);

            Assert.Equal(6L, Count(conn, "Rubriques"));
            var ids = ReadStrings(conn, "Rubriques", "Id");
            Assert.Contains("IEP", ids);
            Assert.Contains("PAPP", ids);
            Assert.Contains("ISSRP_45", ids);
            Assert.Contains("ISSRP_30", ids);
            Assert.Contains("ISSRP_15", ids);
            Assert.Contains("IRG", ids);

            // Toutes insérées (6 Inserees), pas 0.
            Assert.Equal(6, report.Tables.Single(t => t.Table == "Rubriques").Inserees);
        }
    }

    [Fact]
    public async Task Seed_attributs_IEP_et_IRG_sont_conformes()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            // IEP = gain, imposable, cotisable, base TBASE_ECHELON.
            var iepNature = TestSupport.Scalar<string>(conn,
                "SELECT Nature FROM Rubriques WHERE Id = 'IEP';");
            var iepBase = TestSupport.Scalar<string>(conn,
                "SELECT BaseCalcul FROM Rubriques WHERE Id = 'IEP';");
            Assert.Equal("GAIN", iepNature);
            Assert.Equal("TBASE_ECHELON", iepBase);

            // IRG = impot, non imposable, non cotisable.
            var irgNature = TestSupport.Scalar<string>(conn,
                "SELECT Nature FROM Rubriques WHERE Id = 'IRG';");
            Assert.Equal("IMPOT", irgNature);
        }
    }

    // -------------------------------------------------------------------------
    // ReglesEligibilite
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Seed_insere_la_matrice_ISSRP_avec_3_taux()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            // 4 corps en 45% + 4 en 30% + 2 en 15% = 10 règles.
            Assert.Equal(10L, Count(conn, "ReglesEligibilite"));

            // Vérifie que la matrice est bien (RubriqueId, Critere=CORPS, Operateur='=')
            var cnt45 = TestSupport.Scalar<long>(conn, """
                SELECT COUNT(*) FROM ReglesEligibilite
                WHERE RubriqueId = 'ISSRP_45' AND Critere = 'CORPS' AND Operateur = '=';
                """);
            Assert.Equal(4L, cnt45);
        }
    }

    [Fact]
    public async Task Resolution_par_corps_retourne_la_bonne_rubrique_ISSRP()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            // Pour un agent en corps CPDE (Professeurs d'Education), c'est
            // ISSRP_45 qui est éligible.
            var eligible = TestSupport.Scalar<string>(conn, """
                SELECT RubriqueId FROM ReglesEligibilite
                WHERE Critere = 'CORPS' AND Operateur = '=' AND Valeur = 'CPDE'
                  AND DateEffet <= '2025-12-31'
                  AND (DateFin IS NULL OR DateFin >= '2025-12-31');
                """);
            Assert.Equal("ISSRP_45", eligible);

            // Pour un agent en corps CDAE (Adjoints de l'Education), c'est
            // ISSRP_30.
            var eligible30 = TestSupport.Scalar<string>(conn, """
                SELECT RubriqueId FROM ReglesEligibilite
                WHERE Critere = 'CORPS' AND Operateur = '=' AND Valeur = 'CDAE'
                  AND DateEffet <= '2025-12-31'
                  AND (DateFin IS NULL OR DateFin >= '2025-12-31');
                """);
            Assert.Equal("ISSRP_30", eligible30);
        }
    }

    // -------------------------------------------------------------------------
    // Cotisations
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Seed_insere_SS_9_pourcent_comme_dans_Q3b()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            Assert.Equal(3L, Count(conn, "Cotisations"));

            var taux = TestSupport.Scalar<double>(conn,
                "SELECT Taux FROM Cotisations WHERE Code = 'SS';");
            Assert.Equal(0.09, taux);

            var type = TestSupport.Scalar<string>(conn,
                "SELECT TypeCotisation FROM Cotisations WHERE Code = 'SS';");
            Assert.Equal("OBLIGATOIRE_SALARIALE", type);
        }
    }

    [Fact]
    public async Task Seed_insere_mutuelle_et_oeuvres_sociales_comme_facultatives_montant_fixe()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            // Les 2 facultatives ont Taux=NULL (montant fixe) + AssietteRef=MONTANT_FIXE.
            foreach (var code in new[] { "MUTUELLE", "OEUVRES_SOCIALES" })
            {
                var type = TestSupport.Scalar<string>(conn,
                    $"SELECT TypeCotisation FROM Cotisations WHERE Code = '{code}';");
                var taux = TestSupport.Scalar<object?>(conn,
                    $"SELECT Taux FROM Cotisations WHERE Code = '{code}';");
                var assiette = TestSupport.Scalar<string>(conn,
                    $"SELECT AssietteRef FROM Cotisations WHERE Code = '{code}';");

                Assert.Equal("FACULTATIVE", type);
                Assert.Null(taux);
                Assert.Equal("MONTANT_FIXE", assiette);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Paramètres
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Seed_insere_le_parametre_ARRONDI_MODE_par_defaut_DINAR_PLUS_PROCHE()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            Assert.Equal(4L, Count(conn, "Parametres"));

            var valeur = TestSupport.Scalar<string>(conn,
                "SELECT Valeur FROM Parametres WHERE Cle = 'ARRONDI_MODE';");
            Assert.Equal("DINAR_PLUS_PROCHE", valeur);

            var type = TestSupport.Scalar<string>(conn,
                "SELECT Type FROM Parametres WHERE Cle = 'ARRONDI_MODE';");
            Assert.Equal("TEXT", type);
        }
    }

    // -------------------------------------------------------------------------
    // Idempotence
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Seed_est_idempotent()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);
            var r2 = await new ReglementaireSeeder().SeedAsync(conn);

            // 2e run : Inserees = 0 partout.
            Assert.All(r2.Tables, t => Assert.Equal(0, t.Inserees));
            Assert.Equal(6L, Count(conn, "Rubriques"));
            Assert.Equal(10L, Count(conn, "ReglesEligibilite"));
            Assert.Equal(3L, Count(conn, "Cotisations"));
            Assert.Equal(4L, Count(conn, "Parametres"));
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private static List<string> ReadStrings(SqliteConnection c, string table, string col)
    {
        var list = new List<string>();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT {col} FROM {table} ORDER BY {col};";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(reader.GetString(0));
        return list;
    }
}
