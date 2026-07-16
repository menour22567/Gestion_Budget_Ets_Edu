using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Tools.Seeding;

namespace PaieEducation.Tests.Integration.Repositories;

/// <summary>
/// Tests de <see cref="BulletinRepository"/> (Phase 5, tâche 4) : persistance
/// du snapshot d'un bulletin validé et immutabilité (ADR-0008).
/// </summary>
public class BulletinRepositoryTests
{
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

    private static async Task<BulletinSnapshot> CalculerEtCapturer(SqliteConnection conn)
    {
        await new ReglementaireSeeder().SeedAsync(conn);
        await new IrgSeeder().SeedAsync(conn);
        await new FormulesSeeder().SeedAsync(conn);

        var repo = new PayrollReadRepository(conn);
        var input = await repo.ChargerAsync(
            Enseignant(), "2025-06-01", VariablesBase,
            new Dictionary<string, decimal> { ["PAPP"] = 0.30m },
            new Dictionary<string, string> { ["CATEGORIE"] = "13" }, ProfilFiscal.Standard);
        var bulletin = new CalculationPipeline(new ArrondiService(ModeArrondi.DinarPlusProche)).Calculer(input.Value);
        return new SnapshotEngine().Capturer(input.Value, bulletin.Value, "2025-06-05T10:00:00.0000000Z");
    }

    [Fact]
    public async Task ValiderAsync_persiste_le_snapshot_avec_les_montants_cles()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedAgentMinimal(scope.Conn, "A-1");
        var snapshot = await CalculerEtCapturer(scope.Conn);

        var repo = new BulletinRepository(scope.Conn);
        var result = await repo.ValiderAsync("A-1", snapshot, new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal(57739.0, SchemaTestSupport.Scalar<double>(
            scope.Conn, "SELECT Net FROM Bulletins WHERE Id = @id;", ("@id", result.Value)));
        Assert.Equal("2025-06-01", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DatePaie FROM Bulletins WHERE Id = @id;", ("@id", result.Value)));

        var snapshotJson = SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT SnapshotJson FROM Bulletins WHERE Id = @id;", ("@id", result.Value));
        Assert.False(string.IsNullOrWhiteSpace(snapshotJson));
    }

    [Fact]
    public async Task ValiderAsync_bulletin_deja_valide_echoue_sans_ecraser_l_existant()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedAgentMinimal(scope.Conn, "A-1");
        var snapshot = await CalculerEtCapturer(scope.Conn);

        var repo = new BulletinRepository(scope.Conn);
        var premier = await repo.ValiderAsync("A-1", snapshot, DateTimeOffset.UtcNow);
        Assert.True(premier.IsSuccess);

        var doublon = await repo.ValiderAsync("A-1", snapshot, DateTimeOffset.UtcNow);

        Assert.True(doublon.IsFailure);
        Assert.Contains("bulletin", doublon.Error.Message, StringComparison.OrdinalIgnoreCase);

        var nbBulletins = SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM Bulletins;");
        Assert.Equal(1, nbBulletins);
    }
}
