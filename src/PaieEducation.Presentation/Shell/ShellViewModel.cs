using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Presentation.Agents;
using PaieEducation.Presentation.Navigation;
using PaieEducation.Presentation.Payroll;
using PaieEducation.Presentation.Referentiels;
using PaieEducation.Presentation.Workbench;

namespace PaieEducation.Presentation.Shell;

/// <summary>
/// ViewModel du Shell (Phase 6, tâche 1 ; onglets multi-documents) : porte la
/// collection des onglets ouverts (<see cref="Onglets"/>), l'onglet actif
/// (<see cref="OngletActif"/>) et les commandes du menu principal. Un onglet
/// « Accueil » permanent et non fermable est ouvert au démarrage ; chaque
/// navigation ultérieure (menu ou drill-down via <see cref="INavigationService"/>)
/// ouvre un nouvel onglet fermable.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    public ObservableCollection<TabViewModel> Onglets { get; } = [];

    [ObservableProperty]
    private TabViewModel? ongletActif;

    public ShellViewModel(INavigationService navigation, AccueilViewModel accueil)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _navigation.TabRequested += requete => OuvrirOnglet(requete.Titre, requete.ViewModel, estFermable: true);
        OuvrirOnglet("Accueil", accueil, estFermable: false);
    }

    private void OuvrirOnglet(string titre, object contenu, bool estFermable)
    {
        var onglet = new TabViewModel(titre, contenu, estFermable, Fermer);
        Onglets.Add(onglet);
        OngletActif = onglet;
    }

    private void Fermer(TabViewModel onglet)
    {
        var index = Onglets.IndexOf(onglet);
        if (index < 0) return;

        Onglets.RemoveAt(index);
        if (OngletActif == onglet)
        {
            OngletActif = Onglets.Count == 0 ? null : Onglets[Math.Min(index, Onglets.Count - 1)];
        }
    }

    [RelayCommand]
    private void NaviguerVersCalculerBulletin() => _navigation.OpenTab<CalculerBulletinViewModel>("Calculer un bulletin");

    [RelayCommand]
    private void NaviguerVersValiderBulletin() => _navigation.OpenTab<ValiderBulletinViewModel>("Valider un bulletin");

    [RelayCommand]
    private void NaviguerVersConsulterBulletin() => _navigation.OpenTab<ConsulterBulletinViewModel>("Consulter un bulletin");

    [RelayCommand]
    private void NaviguerVersListeAgents() => _navigation.OpenTab<ListeAgentsViewModel>("Liste des agents");

    [RelayCommand]
    private void NaviguerVersCreerAgent() => _navigation.OpenTab<CreerAgentViewModel>("Créer un agent");

    [RelayCommand]
    private void NaviguerVersGrilleIndiciaire() => _navigation.OpenTab<GrilleIndiciaireViewModel>("Grille indiciaire");

    [RelayCommand]
    private void NaviguerVersSuggererRubriques() => _navigation.OpenTab<SuggererRubriquesViewModel>("Suggérer des rubriques");

    [RelayCommand]
    private void NaviguerVersWorkbench() => _navigation.OpenTab<WorkbenchPlaceholderViewModel>("Vue d'ensemble");

    [RelayCommand]
    private void NaviguerVersMatriceCouverture() => _navigation.OpenTab<MatriceCouvertureViewModel>("Matrice de couverture");

    [RelayCommand]
    private void NaviguerVersAuditLog() => _navigation.OpenTab<AuditLogViewModel>("Audit & traçabilité");

    [RelayCommand]
    private void NaviguerVersFicheRubrique() => _navigation.OpenTab<FicheRubriqueViewModel>("Fiche rubrique");

    [RelayCommand]
    private void NaviguerVersEditerRubrique() => _navigation.OpenTab<EditerRubriqueViewModel>("Éditer une rubrique");
}
