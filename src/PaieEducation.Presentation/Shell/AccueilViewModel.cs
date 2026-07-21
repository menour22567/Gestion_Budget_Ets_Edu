using CommunityToolkit.Mvvm.Input;
using PaieEducation.Presentation.Agents;
using PaieEducation.Presentation.Navigation;
using PaieEducation.Presentation.Payroll;
using PaieEducation.Presentation.Referentiels;
using PaieEducation.Presentation.Workbench;

namespace PaieEducation.Presentation.Shell;

/// <summary>
/// Écran d'accueil du Shell (onglet permanent, non fermable) : point d'entrée
/// avec lancement rapide vers chaque écran, groupé par famille. Ne porte
/// aucun état métier — se contente d'ouvrir de nouveaux onglets via
/// <see cref="INavigationService"/>, comme <see cref="WorkbenchPlaceholderViewModel"/>.
/// </summary>
public sealed partial class AccueilViewModel
{
    private readonly INavigationService _navigation;

    public AccueilViewModel(INavigationService navigation)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
    }

    [RelayCommand]
    private void OuvrirCalculerBulletin() => _navigation.OpenTab<CalculerBulletinViewModel>("Calculer un bulletin");

    [RelayCommand]
    private void OuvrirValiderBulletin() => _navigation.OpenTab<ValiderBulletinViewModel>("Valider un bulletin");

    [RelayCommand]
    private void OuvrirConsulterBulletin() => _navigation.OpenTab<ConsulterBulletinViewModel>("Consulter un bulletin");

    [RelayCommand]
    private void OuvrirListeAgents() => _navigation.OpenTab<ListeAgentsViewModel>("Liste des agents");

    [RelayCommand]
    private void OuvrirCreerAgent() => _navigation.OpenTab<CreerAgentViewModel>("Créer un agent");

    [RelayCommand]
    private void OuvrirSuggererRubriques() => _navigation.OpenTab<SuggererRubriquesViewModel>("Suggérer des rubriques");

    [RelayCommand]
    private void OuvrirGrilleIndiciaire() => _navigation.OpenTab<GrilleIndiciaireViewModel>("Grille indiciaire");

    [RelayCommand]
    private void OuvrirWorkbench() => _navigation.OpenTab<WorkbenchPlaceholderViewModel>("Vue d'ensemble");

    [RelayCommand]
    private void OuvrirMatriceCouverture() => _navigation.OpenTab<MatriceCouvertureViewModel>("Matrice de couverture");

    [RelayCommand]
    private void OuvrirAuditLog() => _navigation.OpenTab<AuditLogViewModel>("Audit & traçabilité");

    [RelayCommand]
    private void OuvrirFicheRubrique() => _navigation.OpenTab<FicheRubriqueViewModel>("Fiche rubrique");

    [RelayCommand]
    private void OuvrirEditerRubrique() => _navigation.OpenTab<EditerRubriqueViewModel>("Éditer une rubrique");
}
