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
    public async Task Seed_insere_les_8_rubriques_canoniques()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            var report = await new ReglementaireSeeder().SeedAsync(conn);

            Assert.Equal(8L, Count(conn, "Rubriques"));
            var ids = ReadStrings(conn, "Rubriques", "Id");
            Assert.Contains("IEP_FONC", ids);
            Assert.Contains("IEP_CONT", ids);
            Assert.Contains("EXP_PEDAG", ids);
            Assert.Contains("PAPP", ids);
            Assert.Contains("ISSRP_45", ids);
            Assert.Contains("ISSRP_30", ids);
            Assert.Contains("ISSRP_15", ids);
            Assert.Contains("IRG", ids);

            // Toutes insérées (8 Inserees), pas 0.
            Assert.Equal(8, report.Tables.Single(t => t.Table == "Rubriques").Inserees);
        }
    }

    [Fact]
    public async Task Seed_attributs_IEP_EXP_PEDAG_et_IRG_sont_conformes()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            // IEP_FONC = gain, base INDICE_ECHELON (IE × VPI, Q2-rev).
            Assert.Equal("GAIN", TestSupport.Scalar<string>(conn,
                "SELECT Nature FROM Rubriques WHERE Id = 'IEP_FONC';"));
            Assert.Equal("INDICE_ECHELON", TestSupport.Scalar<string>(conn,
                "SELECT BaseCalcul FROM Rubriques WHERE Id = 'IEP_FONC';"));

            // IEP_CONT = gain, base TBASE (taux composite plafonné 60 %).
            Assert.Equal("TBASE", TestSupport.Scalar<string>(conn,
                "SELECT BaseCalcul FROM Rubriques WHERE Id = 'IEP_CONT';"));

            // EXP_PEDAG = gain, base TBASE_ECHELON (4 % × TBASE × échelon).
            Assert.Equal("TBASE_ECHELON", TestSupport.Scalar<string>(conn,
                "SELECT BaseCalcul FROM Rubriques WHERE Id = 'EXP_PEDAG';"));

            // IRG = impot, non imposable, non cotisable.
            Assert.Equal("IMPOT", TestSupport.Scalar<string>(conn,
                "SELECT Nature FROM Rubriques WHERE Id = 'IRG';"));
        }
    }

    [Fact]
    public async Task Seed_PAPP_est_cotisable_et_servie_trimestriellement()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            // Q-02 (14/07/2026) : PAPP imposable ET cotisable.
            Assert.Equal(1L, TestSupport.Scalar<long>(conn,
                "SELECT EstCotisable FROM Rubriques WHERE Id = 'PAPP';"));
            Assert.Equal(1L, TestSupport.Scalar<long>(conn,
                "SELECT EstImposable FROM Rubriques WHERE Id = 'PAPP';"));

            // INC-04 : calculée mensuellement, servie trimestriellement.
            Assert.Equal("MENSUELLE", TestSupport.Scalar<string>(conn,
                "SELECT Periodicite FROM Rubriques WHERE Id = 'PAPP';"));
            Assert.Equal("TRIMESTRIELLE", TestSupport.Scalar<string>(conn,
                "SELECT PeriodiciteVersement FROM Rubriques WHERE Id = 'PAPP';"));

            // Le libellé corrigé (INC-02) : performances pédagogiques, pas pensions.
            var libelle = TestSupport.Scalar<string>(conn,
                "SELECT Libelle FROM Rubriques WHERE Id = 'PAPP';");
            Assert.Contains("performances pédagogiques", libelle);
        }
    }

    [Fact]
    public async Task Seed_insere_les_parametres_IEP_CONT()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            // Q2-rev : taux composite IEP_CONT (1,4 / 0,7 / plafond 60 %).
            Assert.Equal("1.4", TestSupport.Scalar<string>(conn,
                "SELECT Valeur FROM Parametres WHERE Cle = 'IEP_TAUX_PUBLIC_PCT';"));
            Assert.Equal("0.7", TestSupport.Scalar<string>(conn,
                "SELECT Valeur FROM Parametres WHERE Cle = 'IEP_TAUX_PRIVE_PCT';"));
            Assert.Equal("60", TestSupport.Scalar<string>(conn,
                "SELECT Valeur FROM Parametres WHERE Cle = 'IEP_PLAFOND_PCT';"));
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

            // Vérifie que la matrice est bien (RubriqueId, CritereId='CORPS', Operateur='=')
            // R3 (V009) : CritereId FK remplace Critere TEXT — source unique de vérité
            // (cf. CriteresEligibilite.Id = 'CORPS').
            var cnt45 = TestSupport.Scalar<long>(conn, """
                SELECT COUNT(*) FROM ReglesEligibilite
                WHERE RubriqueId = 'ISSRP_45' AND CritereId = 'CORPS' AND Operateur = '=';
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
            // ISSRP_45 qui est éligible. R3 (V009) : CritereId FK remplace Critere TEXT.
            var eligible = TestSupport.Scalar<string>(conn, """
                SELECT RubriqueId FROM ReglesEligibilite
                WHERE CritereId = 'CORPS' AND Operateur = '=' AND Valeur = 'CPDE'
                  AND DateEffet <= '2025-12-31'
                  AND (DateFin IS NULL OR DateFin >= '2025-12-31');
                """);
            Assert.Equal("ISSRP_45", eligible);

            // Pour un agent en corps CDAE (Adjoints de l'Education), c'est
            // ISSRP_30. R3 (V009) : CritereId FK remplace Critere TEXT.
            var eligible30 = TestSupport.Scalar<string>(conn, """
                SELECT RubriqueId FROM ReglesEligibilite
                WHERE CritereId = 'CORPS' AND Operateur = '=' AND Valeur = 'CDAE'
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

            Assert.Equal(7L, Count(conn, "Parametres"));

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
            Assert.Equal(8L, Count(conn, "Rubriques"));
            Assert.Equal(10L, Count(conn, "ReglesEligibilite"));
            Assert.Equal(3L, Count(conn, "Cotisations"));
            Assert.Equal(7L, Count(conn, "Parametres"));
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
