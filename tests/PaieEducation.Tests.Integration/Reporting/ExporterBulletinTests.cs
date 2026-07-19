using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Reporting;
using PaieEducation.Reporting.Documents;
using PaieEducation.Reporting.UseCases;
using PaieEducation.Seeding;

namespace PaieEducation.Tests.Integration.Reporting;

/// <summary>
/// Tests d'intégration du use case <see cref="ExporterBulletin"/> (Phase 7,
/// 7.2a). Vérifie le chemin complet : snapshot validé en base → use case →
/// service → modèle PDF (via registre) → écriture sur disque avec extension
/// forcée si nécessaire. Couvre les chemins nominaux et les échecs explicites.
/// </summary>
public class ExporterBulletinTests
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

    private static async Task<string> SeederCalculerValider(SqliteConnection conn, string agentId)
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
        var bulletin = new CalculationPipeline(new ArrondiService(ModeArrondi.DinarPlusProche), SeuilExoneration, PlafondLissageGeneral).Calculer(input.Value);
        var snapshot = new SnapshotEngine().Capturer(input.Value, bulletin.Value, "2025-06-05T10:00:00.0000000Z");

        return (await new BulletinRepository(conn).ValiderAsync(agentId, snapshot, DateTimeOffset.UtcNow)).Value;
    }

    private static (ReportingService Service, IBulletinReadRepository Lecture) ConstruireService(SqliteConnection conn)
    {
        // On câble ReportingService à la main (sans héberger le bootstrapper
        // WPF) pour avoir un test d'intégration léger, focalisé sur le
        // comportement du use case + service. On enregistre les DEUX
        // modèles de bulletin (V1 et V2) — V2 est le chemin par défaut
        // depuis 7.2b.
        var pdfRenderer = new BulletinPdfRenderer();
        var excelRenderer = new BulletinExcelExporter();
        var registry = new DocumentModelRegistry();
        registry.Register(new BulletinDocumentModelV1(pdfRenderer));
        registry.Register(new BulletinDocumentModelV2(pdfRenderer));
        var service = new ReportingService(registry, excelRenderer);
        var lecture = new BulletinReadRepository(conn);
        return (service, lecture);
    }

    [Fact]
    public async Task ExecuterAsync_cree_un_pdf_sur_disque_au_chemin_demande()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var bulletinId = await SeederCalculerValider(scope.Conn, "A-PILOTE");
        var (service, lecture) = ConstruireService(scope.Conn);
        var useCase = new ExporterBulletin(lecture, service);
        var chemin = Path.Combine(Path.GetTempPath(), $"bulletin-A-PILOTE-{Guid.NewGuid():N}.pdf");

        try
        {
            var result = await useCase.ExecuterAsync(
                new Demande("A-PILOTE", "2025-06-01", FormatDocument.Pdf, chemin));

            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
            Assert.Equal(chemin, result.Value);
            Assert.True(File.Exists(chemin), "Le PDF doit exister sur disque.");
            Assert.True(new FileInfo(chemin).Length > 0);
        }
        finally
        {
            if (File.Exists(chemin)) File.Delete(chemin);
        }
    }

    [Fact]
    public async Task ExecuterAsync_chemin_sans_extension_ajoute_suffixe_pdf()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeederCalculerValider(scope.Conn, "A-PILOTE");
        var (service, lecture) = ConstruireService(scope.Conn);
        var useCase = new ExporterBulletin(lecture, service);
        var chemin = Path.Combine(Path.GetTempPath(), $"bulletin-sans-ext-{Guid.NewGuid():N}");

        try
        {
            var result = await useCase.ExecuterAsync(
                new Demande("A-PILOTE", "2025-06-01", FormatDocument.Pdf, chemin));

            Assert.True(result.IsSuccess);
            Assert.Equal(chemin + ".pdf", result.Value);
            Assert.True(File.Exists(chemin + ".pdf"));
        }
        finally
        {
            if (File.Exists(chemin + ".pdf")) File.Delete(chemin + ".pdf");
        }
    }

    [Fact]
    public async Task ExecuterAsync_sans_bulletin_valide_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        // Pas de bulletin validé en base.
        var (service, lecture) = ConstruireService(scope.Conn);
        var useCase = new ExporterBulletin(lecture, service);
        var chemin = Path.Combine(Path.GetTempPath(), $"bulletin-vide-{Guid.NewGuid():N}.pdf");

        var result = await useCase.ExecuterAsync(
            new Demande("A-INEXISTANT", "2025-06-01", FormatDocument.Pdf, chemin));

        Assert.True(result.IsFailure);
        Assert.False(File.Exists(chemin), "Aucun fichier ne doit être créé en cas d'échec.");
    }

    [Fact]
    public async Task Generer_pdf_passe_par_le_registre_de_modeles()
    {
        // Vérifie que ReportingService.Generer(format=PDF) résout bien via
        // DocumentModelRegistry (« bulletin », v1) — c'est le chemin
        // Document Engine promis en 7.1.
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeederCalculerValider(scope.Conn, "A-PILOTE");
        var (service, lecture) = ConstruireService(scope.Conn);
        var snapshot = (await lecture.ConsulterAsync("A-PILOTE", "2025-06-01")).Value;

        var bytes = service.Generer(snapshot, FormatDocument.Pdf);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }
}
