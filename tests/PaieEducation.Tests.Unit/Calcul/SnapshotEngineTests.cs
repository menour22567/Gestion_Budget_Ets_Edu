using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Domain.Calcul.ValueObjects;
using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Tests.Unit.Calcul;

/// <summary>
/// Tests du <see cref="SnapshotEngine"/> (RM-105, J4.d) : la capture ne fait que
/// regrouper des données déjà immuables, et rejouer <see cref="PayrollInput"/>
/// depuis un snapshot reproduit le même résultat (déterminisme, ADR-0005).
/// </summary>
public class SnapshotEngineTests
{
    private static Fraction F(string s) => Fraction.Parser(s).Value;

    private static IrgReglePeriode Irg2022() => new(
        "IRG-PER-2022", 30000, 0.40m, 1000, 1500,
        F("137/51"), F("27925/8"), F("93/61"), F("81213/41"), 42500,
        new IrgTranche[]
        {
            new(0, 20000, 0m), new(20001, 40000, 0.23m), new(40001, 80000, 0.27m),
            new(80001, 160000, 0.30m), new(160001, 320000, 0.33m), new(320001, null, 0.35m),
        });

    private static AgentContext Agent() => new(
        Filiere: "ENSEIGNANT", Corps: "PEM", Grade: null, Categorie: 13, Echelon: 5,
        AncienneteAnnees: 10, Fonction: null, TypeContrat: "STATUTAIRE",
        TypeEtablissement: null, OrigineStatutaire: "ENSEIGNANT",
        Note: 0.30m, ValeurPointIndiciaire: 45m, AssietteCotisable: null, AssietteImposable: null);

    private static PayrollInput Input() => new(
        Agent: Agent(),
        DatePaie: "2025-06-01",
        Variables: new Dictionary<string, decimal>
        {
            ["INDICE_MIN"] = 578m, ["INDICE_ECH"] = 100m, ["VPI"] = 45m,
            ["TBASE"] = 26010m, ["TRT"] = 30510m, ["ECH"] = 5m, ["CAT"] = 13m,
        },
        SourcesValeur: new Dictionary<string, decimal>(),
        ClesBareme: new Dictionary<string, string>(),
        Rubriques: new[]
        {
            new RubriqueCalcul("TRAITEMENT", NatureRubrique.Gain, "(INDICE_MIN + INDICE_ECH) * VPI", true, true, 100),
        },
        Baremes: Array.Empty<BaremeValue>(),
        Conditions: Array.Empty<ConditionEligibilite>(),
        Criteres: new Dictionary<string, CritereEligibilite>(),
        Cotisations: Array.Empty<CotisationCalcul>(),
        Profil: ProfilFiscal.Standard,
        RegleIrg: Irg2022());

    [Fact]
    public void Capturer_regroupe_input_et_resultat_sans_les_modifier()
    {
        var pipeline = new CalculationPipeline(new ArrondiService());
        var input = Input();
        var bulletin = pipeline.Calculer(input).Value;

        var snapshot = new SnapshotEngine().Capturer(input, bulletin, "2025-06-05T10:00:00Z");

        Assert.Same(input, snapshot.Input);
        Assert.Same(bulletin, snapshot.Resultat);
        Assert.Equal("2025-06-05T10:00:00Z", snapshot.CapturesLe);
    }

    [Fact]
    public void Rejouer_le_snapshot_reproduit_le_meme_bulletin()
    {
        var pipeline = new CalculationPipeline(new ArrondiService());
        var input = Input();
        var bulletin = pipeline.Calculer(input).Value;
        var snapshot = new SnapshotEngine().Capturer(input, bulletin, "2025-06-05T10:00:00Z");

        // Nouveau pipeline (aucun état partagé) rejouant exactement l'entrée capturée.
        var rejoue = new CalculationPipeline(new ArrondiService()).Calculer(snapshot.Input);

        Assert.True(rejoue.IsSuccess);
        Assert.Equal(bulletin.Net, rejoue.Value.Net);
        Assert.Equal(bulletin.TotalGains, rejoue.Value.TotalGains);
        Assert.Equal(bulletin.Lignes.Count, rejoue.Value.Lignes.Count);
    }
}
