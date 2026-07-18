using PaieEducation.Domain.Calcul.Cotisations;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Domain.Calcul.ValueObjects;
using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Shared.Money;

namespace PaieEducation.Tests.Unit.Calcul;

/// <summary>
/// Test d'orchestration du CalculationPipeline sur un bulletin enseignant
/// complet (formules → éligibilité DNF → assiettes → SS → IRG → net), calculé
/// à la main. Prouve le chaînage des calculateurs J4.a/J4.b.
/// </summary>
public class CalculationPipelineTests
{
    // Valeurs par défaut (seedées dans Parametres, C8.1).
    private const decimal SeuilExoneration = 30000m;
    private const decimal PlafondLissageGeneral = 35000m;

    private static Fraction F(string s) => Fraction.Parser(s).Value;

    private static IrgReglePeriode Irg2022() => new(
        "IRG-PER-2022", 30000, 0.40m, 1000, 1500,
        F("137/51"), F("27925/8"), F("93/61"), F("81213/41"), 42500,
        new IrgTranche[]
        {
            new(0, 20000, 0m), new(20001, 40000, 0.23m), new(40001, 80000, 0.27m),
            new(80001, 160000, 0.30m), new(160001, 320000, 0.33m), new(320001, null, 0.35m),
        });

    private static AgentContext Agent(string corps) => new(
        Filiere: "ENSEIGNANT", Corps: corps, Grade: null, Categorie: 13, Echelon: 5,
        AncienneteAnnees: 10, Fonction: null, TypeContrat: "STATUTAIRE",
        TypeEtablissement: null, OrigineStatutaire: "ENSEIGNANT",
        Note: 0.30m, ValeurPointIndiciaire: 45m, AssietteCotisable: null, AssietteImposable: null);

    private static PayrollInput Input(string corps) => new(
        Agent: Agent(corps),
        DatePaie: "2025-06-01",
        Variables: new Dictionary<string, decimal>
        {
            ["INDICE_MIN"] = 578m, ["INDICE_ECH"] = 100m, ["VPI"] = 45m,
            ["TBASE"] = 26010m, ["TRT"] = 30510m, ["ECH"] = 5m, ["CAT"] = 13m,
        },
        SourcesValeur: new Dictionary<string, decimal> { ["PAPP"] = 0.30m },
        ClesBareme: new Dictionary<string, string>(),
        Rubriques: new[]
        {
            new RubriqueCalcul("TRAITEMENT", NatureRubrique.Gain, "(INDICE_MIN + INDICE_ECH) * VPI", true, true, 100),
            new RubriqueCalcul("EXP_PEDAG", NatureRubrique.Gain, "TBASE * 0.04 * ECH", true, true, 210),
            new RubriqueCalcul("PAPP", NatureRubrique.Gain, "TRT * valeurSource(PAPP)", true, true, 220),
            new RubriqueCalcul("ISSRP_45", NatureRubrique.Gain, "TRT * 0.45", true, true, 230),
        },
        Baremes: Array.Empty<BaremeValue>(),
        Conditions: new[]
        {
            ConditionEligibilite.Creer("C1", "ISSRP_45", "CORPS", Operateur.Egal, "PEM", null,
                PeriodeReglementaire.Creer("2025-01-01", null)),
        },
        Criteres: new Dictionary<string, CritereEligibilite>
        {
            ["CORPS"] = CritereEligibilite.Creer("CORPS", "Corps", TypeValeurCritere.Enum, SourceResolution.Carriere),
        },
        Cotisations: new[]
        {
            new CotisationCalcul(new CotisationDef("SS", ReferenceAssiette.AssietteCotisable, 0.09m, null), EstSalariale: true),
        },
        Profil: ProfilFiscal.Standard,
        RegleIrg: Irg2022(),
        Dependances: Array.Empty<DependanceArete>());

    [Fact]
    public void Bulletin_enseignant_PEM_complet()
    {
        var pipeline = new CalculationPipeline(new ArrondiService(ModeArrondi.DinarPlusProche), SeuilExoneration, PlafondLissageGeneral);
        var r = pipeline.Calculer(Input("PEM"));
        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        var b = r.Value;

        // Gains : TRAITEMENT 30510, EXP_PEDAG 5202, PAPP 9153, ISSRP_45 13730.
        Assert.Equal(30510m, Ligne(b, "TRAITEMENT"));
        Assert.Equal(5202m, Ligne(b, "EXP_PEDAG"));
        Assert.Equal(9153m, Ligne(b, "PAPP"));
        Assert.Equal(13730m, Ligne(b, "ISSRP_45"));
        Assert.Equal(58595m, b.TotalGains.Amount);

        // SS 9 % sur 58595 = 5273,55 → 5274 ; imposable = 58595 − 5274 = 53321.
        Assert.Equal(5274m, Ligne(b, "SS"));
        Assert.Equal(53321m, b.AssietteImposable.Amount);

        // IRG 2022 standard sur 53321 : brut 8196,67 − abattement 1500 = 6696,67 → 6697.
        Assert.Equal(6697m, b.Irg.Amount);

        // Retenues = 5274 + 6697 = 11971 ; net = 58595 − 11971 = 46624.
        Assert.Equal(11971m, b.TotalRetenues.Amount);
        Assert.Equal(46624m, b.Net.Amount);
    }

    [Fact]
    public void Rubrique_DNF_non_eligible_est_absente_du_bulletin()
    {
        // Corps AUTRE : ISSRP_45 (réservé PEM) inéligible → ligne absente.
        var pipeline = new CalculationPipeline(new ArrondiService(), SeuilExoneration, PlafondLissageGeneral);
        var r = pipeline.Calculer(Input("AUTRE"));
        Assert.True(r.IsSuccess);
        Assert.DoesNotContain(r.Value.Lignes, l => l.RubriqueId == "ISSRP_45");
        // Gains sans ISSRP : 30510 + 5202 + 9153 = 44865.
        Assert.Equal(44865m, r.Value.TotalGains.Amount);
    }

    [Fact]
    public void Determinisme_deux_calculs_identiques()
    {
        var pipeline = new CalculationPipeline(new ArrondiService(), SeuilExoneration, PlafondLissageGeneral);
        var a = pipeline.Calculer(Input("PEM")).Value;
        var b = pipeline.Calculer(Input("PEM")).Value;
        Assert.Equal(a.Net, b.Net);
        Assert.Equal(a.TotalGains, b.TotalGains);
        Assert.Equal(a.Irg, b.Irg);
    }

    [Fact]
    public void Formule_referencant_une_variable_inconnue_echoue()
    {
        var pipeline = new CalculationPipeline(new ArrondiService(), SeuilExoneration, PlafondLissageGeneral);
        var mauvais = Input("PEM") with
        {
            Rubriques = new[]
            {
                new RubriqueCalcul("X", NatureRubrique.Gain, "VARIABLE_ABSENTE * 2", true, true, 100),
            },
        };
        var r = pipeline.Calculer(mauvais);
        Assert.True(r.IsFailure);
    }

    private static decimal Ligne(Bulletin b, string id) =>
        b.Lignes.Single(l => l.RubriqueId == id).Montant.Amount;
}
