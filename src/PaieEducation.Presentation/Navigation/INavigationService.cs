namespace PaieEducation.Presentation.Navigation;

/// <summary>
/// Navigation centralisée du Shell (Phase 6, tâche 1) — ViewModel-first :
/// résout le ViewModel demandé via DI, la Vue correspondante est déterminée
/// automatiquement par un <c>DataTemplate</c> implicite
/// (<c>Presentation/Resources/ViewTemplates.xaml</c>). Aucune fenêtre/Vue
/// n'est manipulée directement ici — MVVM strict, aucune logique de
/// navigation en code-behind.
/// </summary>
public interface INavigationService
{
    /// <summary>Résout <typeparamref name="TViewModel"/> via DI et notifie <see cref="ViewModelChanged"/>.</summary>
    void NavigateTo<TViewModel>() where TViewModel : class;

    /// <summary>
    /// Résout <typeparamref name="TViewModel"/> via DI, laisse <paramref name="configurer"/>
    /// initialiser l'instance (ex. présélection d'un identifiant, déclenchement d'un
    /// chargement) puis notifie <see cref="ViewModelChanged"/> — pour une navigation
    /// « préchargée » depuis un écran vers un autre (drill-down).
    /// </summary>
    void NavigateTo<TViewModel>(Action<TViewModel> configurer) where TViewModel : class;

    /// <summary>Levé après chaque navigation réussie, avec le nouveau ViewModel courant.</summary>
    event Action<object>? ViewModelChanged;
}
