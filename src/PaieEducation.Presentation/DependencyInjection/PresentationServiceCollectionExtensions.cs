using Microsoft.Extensions.DependencyInjection;
using PaieEducation.Presentation.Agents;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Presentation.Navigation;
using PaieEducation.Presentation.Payroll;
using PaieEducation.Presentation.Referentiels;
using PaieEducation.Presentation.Shell;
using PaieEducation.Presentation.Workbench;

namespace PaieEducation.Presentation.DependencyInjection;

/// <summary>
/// Composition Root — enregistrement des services Presentation (Phase 6,
/// tâche 1) : navigation/dialogues (Singleton, un seul Shell par app), Shell
/// (Singleton), écrans (Transient — nouvelle instance à chaque navigation).
/// </summary>
public static class PresentationServiceCollectionExtensions
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<AccueilViewModel>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<ShellWindow>();

        services.AddTransient<CalculerBulletinViewModel>();
        services.AddTransient<ValiderBulletinViewModel>();
        services.AddTransient<ConsulterBulletinViewModel>();
        services.AddTransient<ListeAgentsViewModel>();
        services.AddTransient<FicheAgentViewModel>();
        services.AddTransient<CreerAgentViewModel>();
        services.AddTransient<GrilleIndiciaireViewModel>();
        services.AddTransient<SuggererRubriquesViewModel>();
        services.AddTransient<WorkbenchPlaceholderViewModel>();
        services.AddTransient<MatriceCouvertureViewModel>();
        services.AddTransient<AuditLogViewModel>();
        services.AddTransient<FicheRubriqueViewModel>();
        services.AddTransient<EditerRubriqueViewModel>();

        return services;
    }
}
