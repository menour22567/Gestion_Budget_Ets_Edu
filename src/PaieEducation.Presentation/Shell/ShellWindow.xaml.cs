using System.Windows;

namespace PaieEducation.Presentation.Shell;

/// <summary>
/// Fenêtre principale (Shell), résolue par le conteneur DI (Bootstrapper) —
/// pas de <c>StartupUri</c> ni de construction directe. Le
/// <see cref="ShellViewModel"/> est injecté par constructeur.
/// </summary>
public partial class ShellWindow : Window
{
    public ShellWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }
}
