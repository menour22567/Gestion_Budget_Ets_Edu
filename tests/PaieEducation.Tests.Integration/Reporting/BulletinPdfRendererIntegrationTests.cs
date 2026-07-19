using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Rappels;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Seeding;
using PaieEducation.Shared.Money;
using PaieEducation.Reporting;
using UglyToad.PdfPig;

namespace PaieEducation.Tests.Integration.Reporting;

/// <summary>
/// Tests d'intégration bout-en-bout du bulletin PDF (Phase 7, 7.2a) :
/// migration SQLite → seed réglementaire → bulletin calculé → PDF généré →
/// extraction de texte via PdfPig → vérification des sections clés (entête,
/// totaux, net à payer, section « Rappels »).
/// </summary>
/// <remarks>
/// C'est le test de validation principal de la livraison : il garantit qu'un
/// utilisateur peut ouvrir un PDF produit par l'app et y retrouver les
/// informations métier. PdfPig est utilisé sans dépendance native (C# pur).
/// </remarks>
public class BulletinPdfRendererIntegrationTests
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

    private static async Task<BulletinSnapshot> SeederEtCalculer(SqliteConnection conn, string agentId)
    {
        await new ReglementaireSeeder().SeedAsync(conn);
        await new IrgSeeder().SeedAsync(conn);
        await new FormulesSeeder().SeedAsync(conn);
        SeedAgent(conn, agentId);

        var repo = new PayrollReadRepository(conn);
        var input = await repo.ChargerAsync(
            Enseignant(), "2025-06-01", VariablesBase,
            new Dictionary<string, decimal> { ["PAPP"] = 0.30m },
            new Dictionary<string, string> { ["CATEGORIE"] = "13" }, ProfilFiscal.Standard);
        Assert.True(input.IsSuccess, input.IsFailure ? input.Error.Message : null);

        var pipeline = new CalculationPipeline(new ArrondiService(ModeArrondi.DinarPlusProche), SeuilExoneration, PlafondLissageGeneral);
        var bulletin = pipeline.Calculer(input.Value);
        Assert.True(bulletin.IsSuccess, bulletin.IsFailure ? bulletin.Error.Message : null);

        return new SnapshotEngine().Capturer(input.Value, bulletin.Value, "2025-06-05T10:00:00.0000000Z");
    }

    private static string ExtraireTextePdf(byte[] bytes)
    {
        using var pdf = PdfDocument.Open(bytes);
        return string.Join(' ', pdf.GetPages().Select(p => p.Text));
    }

    [Fact]
    public async Task Bulletin_enseignant_depuis_la_base_genere_un_pdf_avec_textes_cles()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var snapshot = await SeederEtCalculer(scope.Conn, "A-PILOTE");

        var bytes = new BulletinPdfRenderer().Rendre(snapshot);
        var texte = ExtraireTextePdf(bytes);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        // Entête administratif
        Assert.Contains("RÉPUBLIQUE ALGÉRIENNE DÉMOCRATIQUE ET POPULAIRE", texte);
        Assert.Contains("BULLETIN DE PAIE", texte);
        // Rubriques attendues du barème enseignant
        Assert.Contains("TRAITEMENT", texte);
        Assert.Contains("IRG", texte);
        // Totaux
        Assert.Contains("Total gains", texte);
        Assert.Contains("Total retenues", texte);
        Assert.Contains("NET À PAYER", texte);
        // Mention d'inviolabilité
        Assert.Contains("Document généré automatiquement", texte);
    }

    [Fact]
    public async Task Bulletin_enseignant_pdf_contient_le_montant_du_net_au_format_da()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var snapshot = await SeederEtCalculer(scope.Conn, "A-PILOTE");

        var bytes = new BulletinPdfRenderer().Rendre(snapshot);
        var texte = ExtraireTextePdf(bytes);

        // 57 739,00 DA — le net de référence du pilote (cf. BulletinEndToEndTests).
        // On tolère le format avec ou sans espace fine/insécable (QuestPDF peut
        // utiliser U+202F ou U+00A0 pour les séparateurs de milliers).
        Assert.Matches(@"57\s*739[,\.]00 DA", texte);
    }

    [Fact]
    public async Task Bulletin_avec_rappels_affiche_la_section_rappels_avec_les_lignes()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var snapshot = await SeederEtCalculer(scope.Conn, "A-PILOTE");

        var rappels = new List<LigneRappel>
        {
            new("TRAITEMENT", new Money(30000m), new Money(30510m), new Money(510m)),
            new("IRG", new Money(10500m), new Money(10807m), new Money(307m)),
        };
        var bytes = new BulletinPdfRenderer().Rendre(snapshot, rappels);
        var texte = ExtraireTextePdf(bytes);

        Assert.Contains("Rappels", texte);
        // La colonne "Delta" porte les montants avec signe (+/-). Le format
        // exact dépend de QuestPDF (parfois "510.00 DA" sans signe si le
        // delta est positif) — on vérifie la présence des valeurs.
        Assert.Contains("510", texte);
        Assert.Contains("307", texte);
    }

    [Fact]
    public async Task Bulletin_sans_rappels_affiche_la_section_vide_avec_mention_explicite()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var snapshot = await SeederEtCalculer(scope.Conn, "A-PILOTE");

        // Pas de rappels (paramètre par défaut).
        var bytes = new BulletinPdfRenderer().Rendre(snapshot);
        var texte = ExtraireTextePdf(bytes);

        Assert.Contains("Rappels", texte);
        Assert.Contains("Aucun rappel", texte);
    }

    [Fact]
    public async Task Pdf_genere_par_le_model_via_le_registre_contient_les_textes_cles()
    {
        // Test du chemin Document Engine : passer par le DocumentModelRegistry
        // (le « 1 » dans Generer() du ReportingService) plutôt que par le
        // renderer direct. Garantit que l'indirection registry → model →
        // renderer fonctionne bout en bout.
        using var scope = SchemaTestSupport.CreateMigrated();
        var snapshot = await SeederEtCalculer(scope.Conn, "A-PILOTE");

        var pdfRenderer = new BulletinPdfRenderer();
        var model = new BulletinDocumentModelV1(pdfRenderer);
        var registry = new PaieEducation.Reporting.Documents.DocumentModelRegistry();
        registry.Register(model);

        var resolved = registry.Resolve<BulletinSnapshot>("bulletin", 1);
        Assert.Same(model, resolved);

        var bytes = resolved.Render(snapshot);
        var texte = ExtraireTextePdf(bytes);

        Assert.Contains("BULLETIN DE PAIE", texte);
        Assert.Contains("NET À PAYER", texte);
    }
}
