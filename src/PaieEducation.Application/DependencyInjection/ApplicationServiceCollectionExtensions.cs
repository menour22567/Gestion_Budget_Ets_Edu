using Microsoft.Extensions.DependencyInjection;
using PaieEducation.Application.Agents.UseCases;
using PaieEducation.Application.Payroll.Services;
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
        services.AddTransient<ConsulterFicheAgent>();
        services.AddTransient<ModifierAgent>();
        services.AddTransient<EnregistrerEvenementCarriere>();
        services.AddTransient<DefinirAttributAgent>();
        services.AddTransient<CalculerBulletin>();
        services.AddTransient<CalculEntreeResolver>();
        services.AddTransient<ValiderBulletin>();
        services.AddTransient<ConsulterBulletin>();
        services.AddTransient<ListerRappels>();
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
        services.AddTransient<ListerAuditLog>();
        services.AddTransient<ConsulterFicheRubrique>();
        services.AddTransient<DefinirRubrique>();
        services.AddTransient<DefinirFormuleRubrique>();
        services.AddTransient<DefinirParametreRubrique>();
        services.AddTransient<DefinirValeurBareme>();
        services.AddTransient<DefinirGroupeEligibilite>();
        services.AddTransient<CloreGroupeEligibilite>();
        services.AddTransient<DefinirRegleEligibilite>();
        services.AddTransient<CloreRegleEligibilite>();
        services.AddTransient<ListerCriteresEligibilite>();

        return services;
    }
}
