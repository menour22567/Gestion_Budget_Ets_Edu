using System.Text.Json;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Infrastructure.Serialization;
using PaieEducation.Tools.Seeding;

namespace PaieEducation.Tests.Integration.Serialization;

/// <summary>
/// Preuve directe de l'exigence ADR-0008 : un <see cref="BulletinSnapshot"/>
/// sérialisé puis désérialisé doit rejouer
/// <see cref="CalculationPipeline.Calculer"/> à l'identique — jamais une
/// réévaluation du passé, toujours le snapshot figé.
/// </summary>
public class BulletinSnapshotJsonConvertersTests
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

    [Fact]
    public async Task Round_trip_JSON_reproduit_le_bulletin_a_l_identique()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await new ReglementaireSeeder().SeedAsync(scope.Conn);
        await new IrgSeeder().SeedAsync(scope.Conn);
        await new FormulesSeeder().SeedAsync(scope.Conn);

        var repo = new PayrollReadRepository(scope.Conn);
        var input = await repo.ChargerAsync(
            Enseignant(), "2025-06-01", VariablesBase,
            new Dictionary<string, decimal> { ["PAPP"] = 0.30m },
            new Dictionary<string, string> { ["CATEGORIE"] = "13" }, ProfilFiscal.Standard);
        Assert.True(input.IsSuccess, input.IsFailure ? input.Error.Message : null);

        var pipeline = new CalculationPipeline(new ArrondiService(ModeArrondi.DinarPlusProche));
        var bulletin = pipeline.Calculer(input.Value);
        Assert.True(bulletin.IsSuccess, bulletin.IsFailure ? bulletin.Error.Message : null);
        Assert.Equal(57739m, bulletin.Value.Net);

        var snapshot = new SnapshotEngine().Capturer(input.Value, bulletin.Value, "2025-06-05T10:00:00.0000000Z");

        var json = JsonSerializer.Serialize(snapshot, BulletinSnapshotJson.Options);
        var deserialise = JsonSerializer.Deserialize<BulletinSnapshot>(json, BulletinSnapshotJson.Options);
        Assert.NotNull(deserialise);

        // Rejouer le pipeline sur l'Input désérialisé (pas sur snapshot.Resultat
        // directement) : c'est l'exigence ADR-0008 — un rappel recalcule contre
        // le snapshot, jamais contre une réévaluation indépendante.
        var rejoue = pipeline.Calculer(deserialise!.Input);
        Assert.True(rejoue.IsSuccess, rejoue.IsFailure ? rejoue.Error.Message : null);
        Assert.Equal(57739m, rejoue.Value.Net);
        Assert.Equal(bulletin.Value.Net, rejoue.Value.Net);
        Assert.Equal(bulletin.Value.TotalGains, rejoue.Value.TotalGains);
        Assert.Equal(bulletin.Value.Irg, rejoue.Value.Irg);
        Assert.Equal(deserialise.Resultat.Net, rejoue.Value.Net);
    }
}
