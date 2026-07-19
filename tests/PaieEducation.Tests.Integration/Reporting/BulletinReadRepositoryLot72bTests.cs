using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Seeding;

namespace PaieEducation.Tests.Integration.Reporting;

/// <summary>
/// Tests d'intégration des nouvelles méthodes du
/// <see cref="BulletinReadRepository"/> ajoutées en 7.2b :
/// <see cref="IBulletinReadRepository.ConsulterAvecBulletinIdAsync"/> et
/// <see cref="IBulletinReadRepository.ListerPourPeriodeAsync"/>.
/// </summary>
public class BulletinReadRepositoryLot72bTests
{
    private const decimal SeuilExoneration = 30000m;
    private const decimal PlafondLissageGeneral = 35000m;

    private static readonly Dictionary<string, decimal> VariablesBase = new()
    {
        ["INDICE_MIN"] = 578m, ["INDICE_ECH"] = 100m, ["VPI"] = 45m,
        ["TBASE"] = 26010m, ["TRT"] = 30510m, ["ECH"] = 5m, ["CAT"] = 13m,
    };

    private static AgentContext Enseignant() => new(
        Filiere: "ENSEIGNANT", Corps: null, Grade: "PDLP-G105", Categorie: 13, Echelon: 5,
        AncienneteAnnees: 10, Fonction: null, TypeContrat: "STATUTAIRE",
        TypeEtablissement: null, OrigineStatutaire: "ENSEIGNANT",
        Note: 0.30m, ValeurPointIndiciaire: 45m, AssietteCotisable: null, AssietteImposable: null);

    private static void SeedAgent(SqliteConnection c, string agentId) => SchemaTestSupport.Exec(c, """
        INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, CreatedAt)
        VALUES ($id, 'MAT-001', 'Test', 'Agent', '1990-01-01', '2015-09-01', 'M', '2026-01-01T00:00:00Z');
        """, ("$id", agentId));

    private static async Task<BulletinSnapshot> SeederCalculer(SqliteConnection conn)
    {
        await new ReglementaireSeeder().SeedAsync(conn);
        await new IrgSeeder().SeedAsync(conn);
        await new FormulesSeeder().SeedAsync(conn);
        SeedAgent(conn, "A-PILOTE");

        var repo = new PayrollReadRepository(conn);
        var input = await repo.ChargerAsync(
            Enseignant(), "2025-06-01", VariablesBase,
            new Dictionary<string, decimal> { ["PAPP"] = 0.30m },
            new Dictionary<string, string> { ["CATEGORIE"] = "13" }, ProfilFiscal.Standard);
        var pipeline = new CalculationPipeline(new ArrondiService(ModeArrondi.DinarPlusProche), SeuilExoneration, PlafondLissageGeneral);
        return new SnapshotEngine().Capturer(input.Value, pipeline.Calculer(input.Value).Value, "2025-06-05T10:00:00Z");
    }

    [Fact]
    public async Task ConsulterAvecBulletinIdAsync_retourne_le_Guid_du_bulletin()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var snapshot = await SeederCalculer(scope.Conn);
        var ecriture = await new BulletinRepository(scope.Conn).ValiderAsync("A-PILOTE", snapshot, DateTimeOffset.UtcNow);
        Assert.True(ecriture.IsSuccess, ecriture.IsFailure ? ecriture.Error.Message : null);
        var bulletinId = ecriture.Value;

        var lecture = new BulletinReadRepository(scope.Conn);
        var result = await lecture.ConsulterAvecBulletinIdAsync("A-PILOTE", "2025-06-01");

        Assert.True(result.IsSuccess);
        Assert.Equal(bulletinId, result.Value.BulletinId);
        Assert.Equal(snapshot.Input.DatePaie, result.Value.Snapshot.Input.DatePaie);
    }

    [Fact]
    public async Task ConsulterAvecBulletinIdAsync_retourne_NotFound_si_aucun_bulletin()
    {
        using var scope = SchemaTestSupport.CreateMigrated();

        var result = await new BulletinReadRepository(scope.Conn).ConsulterAvecBulletinIdAsync("A-INEXISTANT", "2025-06-01");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ListerPourPeriodeAsync_retourne_les_bulletins_de_l_annee_dans_l_ordre()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedAgent(scope.Conn, "A-PILOTE");
        // On valide 3 bulletins fictifs pour le même agent sur 3 mois.
        for (int mois = 4; mois <= 6; mois++)
        {
            var agent = Enseignant();
            var repo = new PayrollReadRepository(scope.Conn);
            var input = await repo.ChargerAsync(
                agent, $"2025-{mois:D2}-01", VariablesBase,
                new Dictionary<string, decimal> { ["PAPP"] = 0.30m },
                new Dictionary<string, string> { ["CATEGORIE"] = "13" }, ProfilFiscal.Standard);
            var pipeline = new CalculationPipeline(new ArrondiService(ModeArrondi.DinarPlusProche), SeuilExoneration, PlafondLissageGeneral);
            var snap = new SnapshotEngine().Capturer(input.Value, pipeline.Calculer(input.Value).Value, $"2025-{mois:D2}-05T10:00:00Z");
            var ecriture = await new BulletinRepository(scope.Conn).ValiderAsync("A-PILOTE", snap, DateTimeOffset.UtcNow);
            Assert.True(ecriture.IsSuccess, ecriture.IsFailure ? ecriture.Error.Message : null);
        }

        var lecture = new BulletinReadRepository(scope.Conn);
        var liste = await lecture.ListerPourPeriodeAsync("A-PILOTE", "2025-01-01", "2025-12-31");

        Assert.True(liste.IsSuccess);
        Assert.Equal(3, liste.Value.Count);
        Assert.Equal("2025-04-01", liste.Value[0].Input.DatePaie);
    }

    [Fact]
    public async Task ListerPourPeriodeAsync_filtre_par_agent()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedAgent(scope.Conn, "A-AUTRE");
        // Pas de bulletin validé : la liste doit être vide, pas en erreur.
        var result = await new BulletinReadRepository(scope.Conn).ListerPourPeriodeAsync("A-AUTRE", "2025-01-01", "2025-12-31");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}
