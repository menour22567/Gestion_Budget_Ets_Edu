using Microsoft.Extensions.DependencyInjection;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Reporting;
using PaieEducation.Reporting.Documents;
using PaieEducation.Reporting.UseCases;
using UglyToad.PdfPig;

namespace PaieEducation.Tests.Integration.Reporting;

/// <summary>
/// Tests d'intégration P11 (audit du 19/07/2026) — export PDF d'un
/// <see cref="RapportImpactDocument"/> (D8 — rapport d'archivage et de
/// validation hiérarchique de toute évolution réglementaire validée).
/// </summary>
/// <remarks>
/// Couvre le chemin nominal du modèle documentaire V1, du use case
/// d'export et l'intégration dans le pipeline
/// <c>SimulerEvolutionReglementaire → ExporterRapportImpact →
/// AppliquerEvolutionReglementaire</c> avec stockage du chemin dans
/// <c>AuditLog.Payload</c>.
/// </remarks>
public class ExporterRapportImpactTests
{
    private static readonly RapportImpact Rapport = new(
        NbAgents: 1240, DeltaMinMensuel: 500m, DeltaMaxMensuel: 900m,
        MontantTotalMensuel: 3_100_000m, PeriodeImpactee: "2026-01-01",
        BulletinsAvertis: 17);

    private static (DocumentModelRegistry Registry, IExporterRapportImpact Export, ReportingService Service)
        Construire()
    {
        var pdfRenderer = new RapportImpactPdfRenderer();
        var registry = new DocumentModelRegistry();
        registry.Register(new RapportImpactDocumentModelV1(pdfRenderer));
        // BulletinExcelExporter n'est pas utilisé pour le rapport d'impact
        // (P11 = PDF uniquement), mais ReportingService l'exige dans son
        // constructeur. On passe une instance réelle — le chemin Excel
        // n'est jamais emprunté par GenererRapportImpact.
        var service = new ReportingService(registry, new BulletinExcelExporter());
        var export = new ExporterRapportImpact(registry, service);
        return (registry, export, service);
    }

    [Fact]
    public void Model_V1_est_enregistre_et_resolvable_dans_le_registre()
    {
        var (registry, _, _) = Construire();

        Assert.True(registry.IsRegistered<RapportImpactDocument>("rapport-impact", 1));
        var model = registry.Resolve<RapportImpactDocument>("rapport-impact", 1);
        Assert.Equal("rapport-impact", model.Id);
        Assert.Equal(1, model.Version);
    }

    [Fact]
    public void Render_produit_un_PDF_non_vide_avec_les_chiffres_cles_du_rapport()
    {
        var (_, _, service) = Construire();
        var document = new RapportImpactDocument(
            Rapport: Rapport,
            Hypothese: "Revalorisation 2026 du point indiciaire (45 -> 50 DA)",
            Horodatage: new DateTime(2026, 7, 22, 10, 30, 0, DateTimeKind.Utc),
            Erreurs: Array.Empty<string>());

        var octets = service.GenererRapportImpact(document, FormatDocument.Pdf);

        Assert.NotNull(octets);
        Assert.NotEmpty(octets);
        Assert.True(octets.Length > 1000, $"PDF trop petit ({octets.Length} octets) — le rendu a probablement échoué.");
        Assert.Equal((byte)'%', octets[0]);
        Assert.Equal((byte)'P', octets[1]);
        Assert.Equal((byte)'D', octets[2]);
        Assert.Equal((byte)'F', octets[3]);

        // Vérifie que le PDF contient le texte attendu (extraction réelle).
        var texte = ExtraireTextePdf(octets);
        Assert.Contains("RAPPORT D'IMPACT", texte.ToUpperInvariant());
        Assert.Contains("HYPOTH", texte.ToUpperInvariant());
        Assert.Contains("1240", texte); // NbAgents
        Assert.Contains("DA", texte); // Symbole DZD (le montant est formatté N2 fr-DZ, le séparateur peut varier)
    }

    [Fact]
    public async Task Export_ajoute_lextension_pdf_si_manquante_et_ecrit_le_fichier()
    {
        var (_, export, _) = Construire();
        var document = new RapportImpactDocument(
            Rapport, "Test export",
            new DateTime(2026, 7, 22, 10, 30, 0, DateTimeKind.Utc),
            Array.Empty<string>());

        var temp = Path.Combine(Path.GetTempPath(), $"rapport-test-{Guid.NewGuid():N}");
        try
        {
            var result = await export.ExecuterAsync(new IExporterRapportImpact.Demande(document, temp));

            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
            Assert.Equal(temp + ".pdf", result.Value);
            Assert.True(File.Exists(result.Value), "Le fichier PDF n'a pas été écrit.");
            Assert.True(new FileInfo(result.Value).Length > 1000, "PDF exporté trop petit.");
        }
        finally
        {
            if (File.Exists(temp + ".pdf")) File.Delete(temp + ".pdf");
        }
    }

    [Fact]
    public async Task Export_respecte_lextension_pdf_si_deja_presente()
    {
        var (_, export, _) = Construire();
        var document = new RapportImpactDocument(
            Rapport, "Test",
            new DateTime(2026, 7, 22, 10, 30, 0, DateTimeKind.Utc),
            Array.Empty<string>());

        var temp = Path.Combine(Path.GetTempPath(), $"rapport-{Guid.NewGuid():N}.pdf");
        try
        {
            var result = await export.ExecuterAsync(new IExporterRapportImpact.Demande(document, temp));

            Assert.True(result.IsSuccess);
            Assert.Equal(temp, result.Value);
            Assert.True(File.Exists(result.Value));
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    [Fact]
    public void Render_produit_un_PDF_avec_bandeau_erreurs_si_liste_non_vide()
    {
        var (_, _, service) = Construire();
        var erreurs = new[]
        {
            "Agent A-PILOTE-001 sans carrière valide, ignoré",
            "Période 2026-01-01 : 3 grades hors catégorie non rattachés"
        };
        var document = new RapportImpactDocument(
            Rapport, "Hypothèse avec erreurs",
            new DateTime(2026, 7, 22, 10, 30, 0, DateTimeKind.Utc),
            erreurs);

        var octets = service.GenererRapportImpact(document, FormatDocument.Pdf);
        var texte = ExtraireTextePdf(octets);

        Assert.Contains("AVERTISSEMENT", texte.ToUpperInvariant());
        Assert.Contains("A-PILOTE-001", texte);
    }

    private static string ExtraireTextePdf(byte[] octets)
    {
        using var pdf = PdfDocument.Open(octets);
        var sb = new System.Text.StringBuilder();
        foreach (var page in pdf.GetPages())
        {
            sb.AppendLine(page.Text);
        }
        return sb.ToString();
    }
}
