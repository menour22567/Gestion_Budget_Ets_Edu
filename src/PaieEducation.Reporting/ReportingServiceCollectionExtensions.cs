using Microsoft.Extensions.DependencyInjection;
using PaieEducation.Reporting.UseCases;

namespace PaieEducation.Reporting;

/// <summary>
/// Composition Root du module Reporting : enregistre les renderers, le
/// service d'orchestration et le use case d'export. Le Bootstrapper/
/// Presentation appelle <see cref="AddReporting"/> une fois (idempotent).
/// </summary>
public static class ReportingServiceCollectionExtensions
{
    public static IServiceCollection AddReporting(this IServiceCollection services)
    {
        services.AddSingleton<BulletinPdfRenderer>();
        services.AddSingleton<BulletinExcelExporter>();
        services.AddSingleton<ReportingService>();
        services.AddTransient<IExporterBulletin, ExporterBulletin>();
        return services;
    }
}
