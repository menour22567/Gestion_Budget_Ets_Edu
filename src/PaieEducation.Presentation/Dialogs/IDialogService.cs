namespace PaieEducation.Presentation.Dialogs;

/// <summary>Boîtes de dialogue centralisées du Shell (Phase 6, tâche 1).</summary>
public interface IDialogService
{
    /// <summary>Affiche une confirmation Oui/Non ; renvoie <c>true</c> si l'utilisateur confirme.</summary>
    Task<bool> ConfirmAsync(string titre, string message);

    /// <summary>Affiche un message d'erreur.</summary>
    Task ShowErrorAsync(string message);
}
