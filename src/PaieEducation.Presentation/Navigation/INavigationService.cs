namespace PaieEducation.Presentation.Navigation;

/// <summary>
/// Navigation centralisée du Shell (Phase 6, tâche 1 ; onglets multi-documents) —
/// ViewModel-first : résout le ViewModel demandé via DI, la Vue correspondante est
/// déterminée automatiquement par un <c>DataTemplate</c> implicite
/// (<c>Presentation/Resources/ViewTemplates.xaml</c>). Aucune fenêtre/Vue
/// n'est manipulée directement ici — MVVM strict, aucune logique de
/// navigation en code-behind.
/// </summary>
public interface INavigationService
{
    /// <summary>Résout <typeparamref name="TViewModel"/> via DI et demande l'ouverture d'un nouvel onglet intitulé <paramref name="titre"/>.</summary>
    void OpenTab<TViewModel>(string titre) where TViewModel : class;

    /// <summary>
    /// Résout <typeparamref name="TViewModel"/> via DI, laisse <paramref name="configurer"/>
    /// initialiser l'instance (ex. présélection d'un identifiant, déclenchement d'un
    /// chargement) puis demande l'ouverture d'un nouvel onglet intitulé <paramref name="titre"/> —
    /// pour une navigation « préchargée » depuis un écran vers un autre (drill-down).
    /// </summary>
    void OpenTab<TViewModel>(string titre, Action<TViewModel> configurer) where TViewModel : class;

    /// <summary>Levé après chaque résolution réussie, avec le titre et le ViewModel du nouvel onglet demandé.</summary>
    event Action<TabRequest>? TabRequested;
}
