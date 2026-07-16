using System.Diagnostics;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Infrastructure.Repositories.Workbench;
using PaieEducation.Persistence.Migrations;

namespace PaieEducation.Tests.Integration.Workbench;

/// <summary>
/// Tests du <see cref="WorkbenchReadCache"/> (J4.e § 7.4, lot 2-restes) :
/// mémorisation, invalidation explicite après écriture de paramétrage, et
/// critère de performance (lectures cachées + évaluation).
/// </summary>
public class WorkbenchReadCacheTests
{
    private const string ResourcePrefix = "PaieEducation.Persistence.Migrations.";
    private const string DatePaie = "2025-06-15";

    private static SqliteConnection CreateMigrated()
    {
        var db = new TempSqliteDb();
        var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        var migrator = new SqliteMigrator(
            new SqliteMigratorOptions(db.ConnectionString, "test"),
            MigrationLoader.LoadFromAssembly(typeof(SqliteMigrator).Assembly, ResourcePrefix));
        var result = migrator.Apply();
        if (result.IsFailure) throw new InvalidOperationException("Migration failed: " + result.Error);
        return conn;
    }

    private static void SeedRubriqueEtCondition(SqliteConnection conn)
    {
        Exec(conn, """
            INSERT INTO Rubriques
                (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul, CreatedAt, Hash)
            VALUES ('RUB_CACHE', 'Rubrique test cache', 'GAIN', 'TRAITEMENT',
                    'MENSUELLE', 1, '2026-01-01T00:00:00Z', 'h');
            """);
        Exec(conn, """
            INSERT INTO ReglesEligibilite
                (Id, RubriqueId, CritereId, Operateur, Valeur, DateEffet, Source, Hash, CreatedAt)
            VALUES ('RE-CACHE-1', 'RUB_CACHE', 'CORPS', '=', 'PEM',
                    '2025-01-01', 'test', 'h', '2026-01-01T00:00:00Z');
            """);
    }

    [Fact]
    public async Task Cache_memorise_puis_invalidation_recharge_depuis_la_base()
    {
        await using var conn = CreateMigrated();
        SeedRubriqueEtCondition(conn);

        var repo = new WorkbenchReadRepository(conn);
        var cache = new WorkbenchReadCache(repo);

        // 1. Premier accès : chargement depuis la base.
        var v1 = await cache.ListerConditionsParRubriqueAsync("RUB_CACHE", DatePaie);
        Assert.Single(v1);

        // 2. Écriture directe en base (simule un chemin d'écriture de paramétrage).
        Exec(conn, """
            INSERT INTO ReglesEligibilite
                (Id, RubriqueId, CritereId, Operateur, Valeur, DateEffet, Source, Hash, CreatedAt)
            VALUES ('RE-CACHE-2', 'RUB_CACHE', 'ECHELON', '>=', '5',
                    '2025-01-01', 'test', 'h', '2026-01-01T00:00:00Z');
            """);

        // 3. Sans invalidation : le cache sert la valeur mémorisée…
        var v2 = await cache.ListerConditionsParRubriqueAsync("RUB_CACHE", DatePaie);
        Assert.Single(v2);
        // … alors que la base en contient bien 2 (lecture directe).
        var direct = await repo.ListerConditionsParRubriqueAsync("RUB_CACHE", DatePaie);
        Assert.Equal(2, direct.Count);

        // 4. Après invalidation : rechargement depuis la base.
        cache.Invalider();
        var v3 = await cache.ListerConditionsParRubriqueAsync("RUB_CACHE", DatePaie);
        Assert.Equal(2, v3.Count);
    }

    [Fact]
    public async Task Cache_criteres_globaux_memorises_et_invalidables()
    {
        await using var conn = CreateMigrated();
        var repo = new WorkbenchReadRepository(conn);
        var cache = new WorkbenchReadCache(repo);

        // Seed V009 : 10 critères actifs.
        var v1 = await cache.ListerCriteresParIdAsync();
        Assert.Equal(10, v1.Count);

        // Nouveau critère inséré directement (test d'extensibilité D3/Q7 : une
        // ligne de dictionnaire, pas de migration).
        Exec(conn, """
            INSERT INTO CriteresEligibilite
                (Id, Libelle, TypeValeur, SourceResolution, Actif, CreatedAt, CreatedBy)
            VALUES ('ZONE', 'Zone géographique', 'ENUM', 'ATTRIBUT_AGENT', 1,
                    '2026-01-01T00:00:00Z', 'test');
            """);

        Assert.Equal(10, (await cache.ListerCriteresParIdAsync()).Count);  // mémorisé
        cache.Invalider();
        Assert.Equal(11, (await cache.ListerCriteresParIdAsync()).Count);  // rechargé
    }

    [Fact]
    public async Task Cache_cles_distinctes_par_date_de_paie()
    {
        await using var conn = CreateMigrated();
        SeedRubriqueEtCondition(conn);   // condition à partir du 2025-01-01
        var cache = new WorkbenchReadCache(new WorkbenchReadRepository(conn));

        // Deux dates ≠ deux entrées de cache : avant la DateEffet, aucune condition.
        Assert.Single(await cache.ListerConditionsParRubriqueAsync("RUB_CACHE", "2025-06-15"));
        Assert.Empty(await cache.ListerConditionsParRubriqueAsync("RUB_CACHE", "2024-06-15"));
    }

    [Fact]
    public async Task Performance_1000_lectures_cachees_plus_evaluations()
    {
        // Critère J4.e § 7.4 (étend le critère BaremeResolver « 1000 résolutions
        // < 50 ms ») : borne large ×5 pour absorber la variance machine — l'ordre
        // de grandeur visé reste la milliseconde par évaluation cachée.
        await using var conn = CreateMigrated();
        SeedRubriqueEtCondition(conn);
        var cache = new WorkbenchReadCache(new WorkbenchReadRepository(conn));
        var evaluator = new RegleEligibiliteEvaluator(new CritereEligibiliteResolver());
        var agent = new AgentContext(Filiere: "ENSEIGNANT", Corps: "PEM", Grade: null,
            Categorie: 7, Echelon: 5, AncienneteAnnees: 10, Fonction: null,
            TypeContrat: "STATUTAIRE", TypeEtablissement: null, OrigineStatutaire: "ENSEIGNANT",
            Note: 0.35m, ValeurPointIndiciaire: 45m,
            AssietteCotisable: null, AssietteImposable: null);

        // Amorçage (premier chargement hors mesure).
        var criteres = await cache.ListerCriteresParIdAsync();
        _ = await cache.ListerConditionsParRubriqueAsync("RUB_CACHE", DatePaie);

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 1000; i++)
        {
            var conditions = await cache.ListerConditionsParRubriqueAsync("RUB_CACHE", DatePaie);
            var r = evaluator.Evaluer("RUB_CACHE", agent, DatePaie, conditions, criteres);
            Assert.True(r.EstEligible);
        }
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 250,
            $"1000 lectures cachées + évaluations ont pris {sw.ElapsedMilliseconds} ms (attendu < 250 ms).");
    }

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
