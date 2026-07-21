namespace PaieEducation.Presentation.Navigation;

/// <summary>Demande d'ouverture d'un nouvel onglet dans le Shell : titre affiché et ViewModel d'écran résolu.</summary>
public sealed record TabRequest(string Titre, object ViewModel);
