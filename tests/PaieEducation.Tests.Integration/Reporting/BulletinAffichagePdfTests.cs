using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
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
/// Tests d'intégration V2 du bulletin PDF (Phase 7, 7.2b) : extraction de
/// texte et vérification que les nouvelles sections (BulletinId, période
/// lisible, cumuls annuels, mentions réglementaires) apparaissent dans le
/// PDF rendu par <see cref="BulletinDocumentModelV2"/>.
/// </summary>
public class BulletinAffichagePdfTests
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

    private static byte[] RendreV2(BulletinAffichage affichage)
    {
        var pdfRenderer = new BulletinPdfRenderer();
        var model = new BulletinDocumentModelV2(pdfRenderer);
        return model.Render(affichage);
    }

    [Fact]
    public async Task V2_BulletinId_apparait_dans_le_pdf()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var snapshot = await SeederEtCalculer(scope.Conn, "A-PILOTE");

        var affichage = new BulletinAffichage(snapshot, BulletinId: "BUL-2025-06-A1B2C3", Cumuls: null);
        var bytes = RendreV2(affichage);
        var texte = ExtraireTextePdf(bytes);

        Assert.Contains("BUL-2025-06-A1B2C3", texte);
    }

    [Fact]
    public async Task V2_periode_lisible_francaise_apparait_dans_le_pdf()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var snapshot = await SeederEtCalculer(scope.Conn, "A-PILOTE");

        var affichage = new BulletinAffichage(snapshot, BulletinId: "BUL-1", Cumuls: null);
        var bytes = RendreV2(affichage);
        var texte = ExtraireTextePdf(bytes);

        // "Juin 2025" — la date 2025-06-01 convertie en libellé FR.
        Assert.Contains("Juin 2025", texte);
    }

    [Fact]
    public async Task V2_avec_cumuls_affiche_la_section_cumuls_avec_les_montants()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var snapshot = await SeederEtCalculer(scope.Conn, "A-PILOTE");

        var cumuls = new CumulsAnnuels(
            Annee: 2025,
            NombreBulletins: 3,
            TotalGains: new Money(225_325m),
            TotalImposable: new Money(200_546m),
            TotalCotisable: new Money(192_000m),
            TotalRetenues: new Money(57_586m),
            TotalIrg: new Money(32_807m),
            TotalNet: new Money(167_739m));
        var affichage = new BulletinAffichage(snapshot, BulletinId: "BUL-1", Cumuls: cumuls);
        var bytes = RendreV2(affichage);
        var texte = ExtraireTextePdf(bytes);

        Assert.Contains("Cumuls depuis le 1er janvier 2025", texte);
        Assert.Contains("3 bulletins validés", texte);
        Assert.Contains("Cumul gains", texte);
        Assert.Contains("Cumul net à payer", texte);
        Assert.Contains("225", texte);  // gains
        Assert.Contains("167", texte);  // net
    }

    [Fact]
    public async Task V2_sans_cumuls_n_affiche_pas_la_section_cumuls()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var snapshot = await SeederEtCalculer(scope.Conn, "A-PILOTE");

        var affichage = new BulletinAffichage(snapshot, BulletinId: "BUL-1", Cumuls: null);
        var bytes = RendreV2(affichage);
        var texte = ExtraireTextePdf(bytes);

        Assert.DoesNotContain("Cumuls depuis le 1er janvier", texte);
        Assert.DoesNotContain("Cumul gains", texte);
    }

    [Fact]
    public async Task V2_cumuls_a_zero_est_traite_comme_absence_de_cumuls()
    {
        // CumulsAnnuels avec NombreBulletins = 0 = cas « pas de bulletin validé
        // cette année ». On ne doit pas afficher une section vide.
        using var scope = SchemaTestSupport.CreateMigrated();
        var snapshot = await SeederEtCalculer(scope.Conn, "A-PILOTE");

        var affichage = new BulletinAffichage(snapshot, BulletinId: "BUL-1", Cumuls: CumulsAnnuels.Vide(2025));
        var bytes = RendreV2(affichage);
        var texte = ExtraireTextePdf(bytes);

        Assert.DoesNotContain("Cumuls depuis le 1er janvier", texte);
    }

    [Fact]
    public async Task V2_mentions_reglementaires_apparaissent_en_pied_de_page()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var snapshot = await SeederEtCalculer(scope.Conn, "A-PILOTE");

        var affichage = new BulletinAffichage(snapshot, BulletinId: "BUL-1", Cumuls: null);
        var bytes = RendreV2(affichage);
        var texte = ExtraireTextePdf(bytes);

        // Les trois mentions réglementaires ajoutées en 7.2b.
        Assert.Contains("conformément à la réglementation", texte, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sans limitation de durée", texte, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rappel", texte, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task V2_BulletinAffichage_via_FromSnapshot_ne_plante_pas_et_n_affiche_pas_de_cumuls()
    {
        // V1 path via FromSnapshot — utilisé par les tests 7.2a et les exports
        // unitaires. Le PDF doit quand même être valide mais sans cumuls ni
        // BulletinId visible.
        using var scope = SchemaTestSupport.CreateMigrated();
        var snapshot = await SeederEtCalculer(scope.Conn, "A-PILOTE");

        var affichage = BulletinAffichage.FromSnapshot(snapshot);
        var bytes = RendreV2(affichage);
        var texte = ExtraireTextePdf(bytes);

        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
        Assert.Contains("BULLETIN DE PAIE", texte);
        Assert.DoesNotContain("Cumuls depuis le 1er janvier", texte);
    }
}
