using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Presentation.Agents;
using PaieEducation.Presentation.Navigation;
using PaieEducation.Presentation.Payroll;
using PaieEducation.Presentation.Referentiels;
using PaieEducation.Presentation.Workbench;

namespace PaieEducation.Presentation.Shell;

/// <summary>
/// ViewModel du Shell (Phase 6, tâche 1) : porte le ViewModel actuellement
/// affiché (<see cref="CurrentViewModel"/>, résolu par
/// <see cref="INavigationService"/>) et les commandes du menu principal.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private object? currentViewModel;

    public ShellViewModel(INavigationService navigation)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _navigation.ViewModelChanged += vm => CurrentViewModel = vm;
        _navigation.NavigateTo<CalculerBulletinViewModel>(); // écran par défaut au démarrage.
    }

    [RelayCommand]
    private void NaviguerVersCalculerBulletin() => _navigation.NavigateTo<CalculerBulletinViewModel>();

    [RelayCommand]
    private void NaviguerVersValiderBulletin() => _navigation.NavigateTo<ValiderBulletinViewModel>();

    [RelayCommand]
    private void NaviguerVersConsulterBulletin() => _navigation.NavigateTo<ConsulterBulletinViewModel>();

    [RelayCommand]
    private void NaviguerVersCreerAgent() => _navigation.NavigateTo<CreerAgentViewModel>();

    [RelayCommand]
    private void NaviguerVersGrilleIndiciaire() => _navigation.NavigateTo<GrilleIndiciaireViewModel>();

    [RelayCommand]
    private void NaviguerVersSuggererRubriques() => _navigation.NavigateTo<SuggererRubriquesViewModel>();

    [RelayCommand]
    private void NaviguerVersWorkbench() => _navigation.NavigateTo<WorkbenchPlaceholderViewModel>();

    [RelayCommand]
    private void NaviguerVersMatriceCouverture() => _navigation.NavigateTo<MatriceCouvertureViewModel>();

    [RelayCommand]
    private void NaviguerVersAuditLog() => _navigation.NavigateTo<AuditLogViewModel>();

    [RelayCommand]
    private void NaviguerVersFicheRubrique() => _navigation.NavigateTo<FicheRubriqueViewModel>();

    [RelayCommand]
    private void NaviguerVersEditerRubrique() => _navigation.NavigateTo<EditerRubriqueViewModel>();
}
