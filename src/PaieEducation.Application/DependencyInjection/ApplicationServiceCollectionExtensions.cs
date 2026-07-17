using Microsoft.Extensions.DependencyInjection;
using PaieEducation.Application.Agents.UseCases;
using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Application.Referentiels.UseCases;
using PaieEducation.Application.Workbench.UseCases;

namespace PaieEducation.Application.DependencyInjection;

/// <summary>
/// Composition Root — enregistrement des use cases Application. Classes
/// légères (dépendent de ports Domain résolus par Infrastructure) :
/// <c>Transient</c>, résolues à l'intérieur d'un scope actif.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddTransient<CreerAgent>();
        services.AddTransient<CalculerBulletin>();
        services.AddTransient<ValiderBulletin>();
        services.AddTransient<ConsulterBulletin>();
        services.AddTransient<DefinirValeurPoint>();
        services.AddTransient<DefinirIndiceMinGrille>();
        services.AddTransient<DefinirIndiceEchelon>();
        services.AddTransient<ListerReferentiels>();
        services.AddTransient<SimulerEvolutionReglementaire>();
        services.AddTransient<SuggererRubriques>();
        services.AddTransient<AccepterSuggestion>();
        services.AddTransient<SupprimerAffectation>();
        services.AddTransient<SuspendreAffectation>();
        services.AddTransient<ListerAffectationsAgent>();
        services.AddTransient<ListerMatriceCouverture>();
        services.AddTransient<GenererRappels>();
        services.AddTransient<DupliquerVersion>();
        services.AddTransient<AppliquerEvolutionReglementaire>();

        return services;
    }
}
