using Microsoft.Extensions.DependencyInjection;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Reporting.Documents;
using PaieEducation.Reporting.UseCases;

namespace PaieEducation.Reporting;

/// <summary>
/// Composition Root du module Reporting : enregistre les renderers, le
/// service d'orchestration, le registre de modèles versionnés (7.1), les
/// modèles bulletin V1 (7.2a) et V2 (7.2b) et le use case d'export. Le
/// Bootstrapper/Presentation appelle <see cref="AddReporting"/> une fois.
/// </summary>
public static class ReportingServiceCollectionExtensions
{
    public static IServiceCollection AddReporting(this IServiceCollection services)
    {
        services.AddSingleton<BulletinPdfRenderer>();
        services.AddSingleton<BulletinExcelExporter>();
        services.AddSingleton<RapportImpactPdfRenderer>();

        // Modèles bulletin V1 et V2 — coexistent dans le registre.
        services.AddSingleton<BulletinDocumentModelV1>();
        services.AddSingleton<BulletinDocumentModelV2>();

        // Modèle rapport d'impact (P11).
        services.AddSingleton<RapportImpactDocumentModelV1>();

        // Registre de modèles : factory qui enregistre les modèles au moment
        // de la première résolution. Les modèles eux-mêmes sont des singletons.
        services.AddSingleton<DocumentModelRegistry>(sp =>
        {
            var registry = new DocumentModelRegistry();
            registry.Register(sp.GetRequiredService<BulletinDocumentModelV1>());
            registry.Register(sp.GetRequiredService<BulletinDocumentModelV2>());
            registry.Register(sp.GetRequiredService<RapportImpactDocumentModelV1>());
            return registry;
        });

        services.AddSingleton<ReportingService>();
        services.AddTransient<IExporterBulletin, ExporterBulletin>();
        services.AddTransient<IExporterRapportImpact, ExporterRapportImpact>();
        return services;
    }
}
