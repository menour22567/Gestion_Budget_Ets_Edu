using CommunityToolkit.Mvvm.Input;
using PaieEducation.Presentation.Navigation;

namespace PaieEducation.Presentation.Workbench;

/// <summary>
/// Hub d'entrée du Workbench réglementaire (Phase 6, tâche 1) : point de
/// navigation central vers les sous-écrans spécialisés (D7). Ne porte aucun
/// état métier — se contente de router vers les ViewModels dédiés via
/// <see cref="INavigationService"/>.
/// </summary>
public sealed partial class WorkbenchPlaceholderViewModel
{
    private readonly INavigationService _navigation;

    public WorkbenchPlaceholderViewModel(INavigationService navigation)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
    }

    [RelayCommand]
    private void AllerVersMatriceCouverture() => _navigation.OpenTab<MatriceCouvertureViewModel>("Matrice de couverture");

    [RelayCommand]
    private void AllerVersFicheRubrique() => _navigation.OpenTab<FicheRubriqueViewModel>("Fiche rubrique");

    [RelayCommand]
    private void AllerVersEditerRubrique() => _navigation.OpenTab<EditerRubriqueViewModel>("Éditer une rubrique");

    [RelayCommand]
    private void AllerVersAuditLog() => _navigation.OpenTab<AuditLogViewModel>("Audit & traçabilité");

    [RelayCommand]
    private void AllerVersSuggererRubriques() => _navigation.OpenTab<SuggererRubriquesViewModel>("Suggérer des rubriques");
}
