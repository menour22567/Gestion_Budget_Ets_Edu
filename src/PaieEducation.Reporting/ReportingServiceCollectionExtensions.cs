using Microsoft.Extensions.DependencyInjection;
using PaieEducation.Reporting.Documents;
using PaieEducation.Reporting.UseCases;

namespace PaieEducation.Reporting;

/// <summary>
/// Composition Root du module Reporting : enregistre les renderers, le
/// service d'orchestration, le registre de modèles versionnés (7.1), le
/// modèle bulletin V1 (7.2a) et le use case d'export. Le Bootstrapper/
/// Presentation appelle <see cref="AddReporting"/> une fois (idempotent).
/// </summary>
public static class ReportingServiceCollectionExtensions
{
    public static IServiceCollection AddReporting(this IServiceCollection services)
    {
        services.AddSingleton<BulletinPdfRenderer>();
        services.AddSingleton<BulletinExcelExporter>();

        // Modèle bulletin V1 — résolu par le registre pour les exports PDF.
        services.AddSingleton<BulletinDocumentModelV1>();

        // Registre de modèles : factory qui enregistre les modèles au moment
        // de la première résolution. Les modèles eux-mêmes sont des singletons.
        services.AddSingleton<DocumentModelRegistry>(sp =>
        {
            var registry = new DocumentModelRegistry();
            registry.Register(sp.GetRequiredService<BulletinDocumentModelV1>());
            return registry;
        });

        services.AddSingleton<ReportingService>();
        services.AddTransient<IExporterBulletin, ExporterBulletin>();
        return services;
    }
}
