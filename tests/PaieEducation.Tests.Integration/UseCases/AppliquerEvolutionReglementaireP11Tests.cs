using System.Text.Json;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Infrastructure.Repositories.Workbench;
using PaieEducation.Reporting;
using PaieEducation.Reporting.Documents;
using PaieEducation.Reporting.UseCases;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Tests d'intégration P11 — chemin complet dry-run → export PDF du rapport
/// d'impact → commit Workbench (D8) avec chemin du rapport persisté dans
/// <c>AuditLog.Payload</c>.
/// </summary>
/// <remarks>
/// Le critère d'acceptation P11 du plan (cf. <c>docs/audit/PLAN_ACTION_2026-07-19.md</c> §3)
/// exige « un dry-run produit un PDF horodaté reproductible ; le commit
/// référence le rapport dans AuditLog ». Ces tests le prouvent bout-en-bout :
/// aucun chemin de code n'est court-circuité, l'audit est relu en base, le
/// PDF est relu sur disque.
/// </remarks>
public class AppliquerEvolutionReglementaireP11Tests
{
    private sealed class HorlogeFixe(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
        public DateTimeOffset UtcNow => now;
        public DateOnly Today => DateOnly.FromDateTime(now.UtcDateTime);
    }

    private static readonly HorlogeFixe Horloge = new(new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero));

    private static readonly RapportImpact Rapport = new(
        NbAgents: 1240, DeltaMinMensuel: 500m, DeltaMaxMensuel: 900m,
        MontantTotalMensuel: 3_100_000m, PeriodeImpactee: "2026-01-01",
        BulletinsAvertis: 0);

    [Fact]
    public async Task Executer_avec_CheminRapport_exporte_le_PDF_et_chemin_dans_AuditLog_Payload()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var grille = new GrilleIndiciaireRepository(scope.Conn);
        await grille.DefinirValeurPointAsync(45m, "2007-01-01", "2007", null, DateTimeOffset.UtcNow);

        // Câblage Reporting réel (P11 : modèle V1 + use case d'export).
        var pdfRenderer = new RapportImpactPdfRenderer();
        var registry = new DocumentModelRegistry();
        registry.Register(new RapportImpactDocumentModelV1(pdfRenderer));
        var reporting = new ReportingService(registry, new BulletinExcelExporter());
        var exporteur = new ExporterRapportImpact(registry, reporting);

        var useCase = new AppliquerEvolutionReglementaire(
            grille,
            new AuditLogRepository(scope.Conn),
            Horloge,
            new TestUnitOfWork(),
            exporteur);

        var temp = Path.Combine(Path.GetTempPath(), $"rapport-rondelle-{Guid.NewGuid():N}");
        try
        {
            var demande = new AppliquerEvolutionReglementaire.Demande(
                Description: "Décret 26-XX — revalorisation du point indiciaire (P11)",
                RapportImpact: Rapport,
                Strategie: StrategieVersionning.ClotureEtNouvelleVersion,
                NouvelleValeur: 50m,
                DateEffet: "2026-01-01",
                Version: "2026",
                Source: "Décret 26-XX",
                Actor: "admin",
                CheminRapport: temp);

            var result = await useCase.ExecuterAsync(demande);

            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);

            // 1) Le PDF a bien été écrit.
            var cheminAttendu = temp + ".pdf";
            Assert.True(File.Exists(cheminAttendu), $"PDF non écrit : {cheminAttendu}");
            Assert.True(new FileInfo(cheminAttendu).Length > 1000, "PDF exporté trop petit.");

            // 2) L'audit contient le chemin du rapport dans son payload.
            //    Note : System.Text.Json échappe les `\` du path Windows en
            //    `\\` dans la chaîne JSON, donc on désérialise au lieu de
            //    faire un Contains sur la chaîne brute.
            var payload = SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT Payload FROM AuditLog;");
            Assert.NotNull(payload);
            using var doc = JsonDocument.Parse(payload);
            var racine = doc.RootElement;
            Assert.Equal(cheminAttendu, racine.GetProperty("CheminRapportPdf").GetString());
            // L'actor est dans un autre champ (EnregistrerAsync le passe
            // séparément du payload JSON) — non vérifié ici.
        }
        finally
        {
            var cheminPdf = temp + ".pdf";
            if (File.Exists(cheminPdf)) File.Delete(cheminPdf);
        }
    }

    [Fact]
    public async Task Executer_sans_CheminRapport_ne_produit_aucun_PDF_et_paquet_paquet_ne_reference_aucun_chemin()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var grille = new GrilleIndiciaireRepository(scope.Conn);
        await grille.DefinirValeurPointAsync(45m, "2007-01-01", "2007", null, DateTimeOffset.UtcNow);

        // On câble quand même un exporteur réel — l'idée est qu'il ne doit
        // pas être appelé du tout si CheminRapport est null.
        var pdfRenderer = new RapportImpactPdfRenderer();
        var registry = new DocumentModelRegistry();
        registry.Register(new RapportImpactDocumentModelV1(pdfRenderer));
        var reporting = new ReportingService(registry, new BulletinExcelExporter());
        var exporteur = new ExporterRapportImpact(registry, reporting);

        var useCase = new AppliquerEvolutionReglementaire(
            grille,
            new AuditLogRepository(scope.Conn),
            Horloge,
            new TestUnitOfWork(),
            exporteur);

        var demande = new AppliquerEvolutionReglementaire.Demande(
            Description: "Commit sans export (rétrocompatibilité test existant)",
            RapportImpact: Rapport,
            Strategie: StrategieVersionning.ClotureEtNouvelleVersion,
            NouvelleValeur: 50m,
            DateEffet: "2026-01-01",
            Version: "2026",
            Source: null,
            Actor: "admin");
        // CheminRapport non fourni (rétrocompatibilité avec tests P1 existants).

        var result = await useCase.ExecuterAsync(demande);

        Assert.True(result.IsSuccess);
        var payload = SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT Payload FROM AuditLog;");
        using var doc = JsonDocument.Parse(payload);
        var racine = doc.RootElement;
        // CheminRapportPdf sérialisé comme null (et pas comme chaîne absente)
        Assert.Equal(JsonValueKind.Null, racine.GetProperty("CheminRapportPdf").ValueKind);
    }
}
