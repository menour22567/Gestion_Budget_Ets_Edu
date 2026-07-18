using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Seeding;

namespace PaieEducation.Tests.Integration.Repositories;

/// <summary>
/// Tests de <see cref="BulletinReadRepository"/> (Phase 5, tâche 4) :
/// relecture du snapshot persisté par <see cref="BulletinRepository"/>.
/// </summary>
public class BulletinReadRepositoryTests
{
    // Valeurs par défaut (seedées dans Parametres, C8.1).
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

    private static void SeedAgentMinimal(SqliteConnection c, string agentId) => SchemaTestSupport.Exec(c, """
        INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, CreatedAt)
        VALUES ($id, 'MAT-001', 'Test', 'Agent', '1990-01-01', '2015-09-01', 'M', '2026-01-01T00:00:00Z');
        """, ("$id", agentId));

    private static async Task<string> SeederEtValider(SqliteConnection conn, string agentId)
    {
        await new ReglementaireSeeder().SeedAsync(conn);
        await new IrgSeeder().SeedAsync(conn);
        await new FormulesSeeder().SeedAsync(conn);
        SeedAgentMinimal(conn, agentId);

        var repo = new PayrollReadRepository(conn);
        var input = await repo.ChargerAsync(
            Enseignant(), "2025-06-01", VariablesBase,
            new Dictionary<string, decimal> { ["PAPP"] = 0.30m },
            new Dictionary<string, string> { ["CATEGORIE"] = "13" }, ProfilFiscal.Standard);
        var bulletin = new CalculationPipeline(new ArrondiService(ModeArrondi.DinarPlusProche), SeuilExoneration, PlafondLissageGeneral).Calculer(input.Value);
        var snapshot = new SnapshotEngine().Capturer(input.Value, bulletin.Value, "2025-06-05T10:00:00.0000000Z");

        var ecriture = await new BulletinRepository(conn).ValiderAsync(agentId, snapshot, DateTimeOffset.UtcNow);
        return ecriture.Value;
    }

    [Fact]
    public async Task ConsulterAsync_relit_le_snapshot_persiste()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeederEtValider(scope.Conn, "A-1");

        var repo = new BulletinReadRepository(scope.Conn);
        var result = await repo.ConsulterAsync("A-1", "2025-06-01");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal(57739m, result.Value.Resultat.Net.Amount);
        Assert.Equal("2025-06-01", result.Value.Input.DatePaie);
    }

    [Fact]
    public async Task ConsulterAsync_aucun_bulletin_valide_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();

        var repo = new BulletinReadRepository(scope.Conn);
        var result = await repo.ConsulterAsync("A-INEXISTANT", "2025-06-01");

        Assert.True(result.IsFailure);
        Assert.Contains("bulletin", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
