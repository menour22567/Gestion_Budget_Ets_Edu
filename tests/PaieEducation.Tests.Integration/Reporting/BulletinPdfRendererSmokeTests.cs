using PaieEducation.Domain.Calcul.Audit;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Rappels;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Domain.Calcul.ValueObjects;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Shared.Money;
using PaieEducation.Reporting;

namespace PaieEducation.Tests.Integration.Reporting;

/// <summary>
/// Tests « smoke » du <see cref="BulletinPdfRenderer"/> (Phase 7, 7.2a).
/// On vérifie que le PDF est produit (signature, taille non nulle) sans
/// planter, sans toucher au contenu textuel — la vérification du contenu
/// est faite par les tests d'intégration avec extraction PdfPig.
/// </summary>
public class BulletinPdfRendererSmokeTests
{
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

    private static AgentContext Agent() => new(
        Filiere: "ENSEIGNANT", Corps: "PEM", Grade: null, Categorie: 13, Echelon: 5,
        AncienneteAnnees: 10, Fonction: null, TypeContrat: "STATUTAIRE",
        TypeEtablissement: null, OrigineStatutaire: "ENSEIGNANT",
        Note: 0.30m, ValeurPointIndiciaire: 45m, AssietteCotisable: null, AssietteImposable: null);

    private static PayrollInput InputMinimal() => new(
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
        Baremes: new List<BaremeValue>(),
        Conditions: new List<ConditionEligibilite>(),
        Criteres: new Dictionary<string, CritereEligibilite>(),
        Cotisations: Array.Empty<CotisationCalcul>(),
        Profil: ProfilFiscal.Standard,
        RegleIrg: Irg2022(),
        Dependances: Array.Empty<DependanceArete>());

    private static BulletinSnapshot SnapshotVide() =>
        // Bulletin calculé mais sans lignes : l'engine émet un IRG et un
        // net à payer à 0 si rien n'est éligible ; on accepte ce cas.
        new BulletinSnapshot(
            InputMinimal(),
            new Bulletin(
                Lignes: Array.Empty<BulletinLigne>(),
                TotalGains: Money.Zero,
                AssietteCotisable: Money.Zero,
                AssietteImposable: Money.Zero,
                TotalRetenues: Money.Zero,
                Irg: Money.Zero,
                Net: Money.Zero,
                Audit: new JournalAudit(Array.Empty<EtapeAudit>())),
            CapturesLe: "2025-06-05T10:00:00Z");

    [Fact]
    public void Rendre_snapshot_minimal_genere_un_pdf_non_vide_avec_signature()
    {
        var renderer = new BulletinPdfRenderer();
        var input = InputMinimal();
        var pipeline = new CalculationPipeline(new ArrondiService(), SeuilExoneration, PlafondLissageGeneral);
        var bulletin = pipeline.Calculer(input).Value;
        var snapshot = new SnapshotEngine().Capturer(input, bulletin, "2025-06-05T10:00:00Z");

        var bytes = renderer.Rendre(snapshot);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0, "Le PDF produit ne doit pas être vide.");
        // Un PDF commence toujours par l'en-tête "%PDF-" (cf. ISO 32000-1).
        var signature = System.Text.Encoding.ASCII.GetString(bytes, 0, Math.Min(5, bytes.Length));
        Assert.Equal("%PDF-", signature);
    }

    [Fact]
    public void Rendre_snapshot_null_leve_ArgumentNullException()
    {
        var renderer = new BulletinPdfRenderer();

        Assert.Throws<ArgumentNullException>(() => renderer.Rendre(null!));
    }

    [Fact]
    public void Rendre_bulletin_vide_produit_un_pdf_valide()
    {
        var renderer = new BulletinPdfRenderer();

        var bytes = renderer.Rendre(SnapshotVide());

        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public void Rendre_avec_rappels_ne_plante_pas_et_reste_un_PDF()
    {
        var renderer = new BulletinPdfRenderer();
        var input = InputMinimal();
        var pipeline = new CalculationPipeline(new ArrondiService(), SeuilExoneration, PlafondLissageGeneral);
        var bulletin = pipeline.Calculer(input).Value;
        var snapshot = new SnapshotEngine().Capturer(input, bulletin, "2025-06-05T10:00:00Z");
        var rappels = new List<LigneRappel>
        {
            new("TRAITEMENT", new Money(30000m), new Money(30510m), new Money(510m)),
        };

        var bytes = renderer.Rendre(snapshot, rappels);

        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public void Rendre_appel_repetitif_produit_des_pdf_egaux_en_taille()
    {
        // Test de déterminisme léger (smoke) : deux rendus consécutifs du
        // même snapshot produisent des PDFs de même taille — l'horodatage du
        // snapshot étant figé, aucune valeur aléatoire ne doit apparaître.
        var renderer = new BulletinPdfRenderer();
        var input = InputMinimal();
        var pipeline = new CalculationPipeline(new ArrondiService(), SeuilExoneration, PlafondLissageGeneral);
        var bulletin = pipeline.Calculer(input).Value;
        var snapshot = new SnapshotEngine().Capturer(input, bulletin, "2025-06-05T10:00:00Z");

        var first = renderer.Rendre(snapshot);
        var second = renderer.Rendre(snapshot);

        Assert.Equal(first.Length, second.Length);
    }
}
