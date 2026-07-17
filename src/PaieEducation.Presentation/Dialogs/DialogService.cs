using System.Windows;

namespace PaieEducation.Presentation.Dialogs;

/// <summary>Implémentation WPF de <see cref="IDialogService"/> (<see cref="MessageBox"/>).</summary>
public sealed class DialogService : IDialogService
{
    public Task<bool> ConfirmAsync(string titre, string message)
    {
        var reponse = MessageBox.Show(message, titre, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return Task.FromResult(reponse == MessageBoxResult.Yes);
    }

    public Task ShowErrorAsync(string message)
    {
        MessageBox.Show(message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        return Task.CompletedTask;
    }
}
