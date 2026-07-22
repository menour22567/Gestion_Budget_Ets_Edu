using System.Windows.Controls;
using System.Windows.Input;
using PaieEducation.Application.Workbench.Services;

namespace PaieEducation.Presentation.Workbench;

/// <summary>
/// Vue WPF de l'écran « Éditer une rubrique » (P10 — FormulaEditor avancé).
/// Le code-behind est strictement minimal (CM/Vue passive) : toute la
/// logique métier est dans <see cref="EditerRubriqueViewModel"/>. Le seul
/// handler présent gère le double-clic sur un item du Popup
/// d'auto-complétion pour l'insérer dans l'expression.
/// </summary>
public sealed partial class EditerRubriqueView : UserControl
{
    public EditerRubriqueView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Double-clic sur un item de la <c>ListBox</c> d'auto-complétion :
    /// délègue au <c>InsererCompletionCommand</c> du ViewModel. Le
    /// DataContext de l'item est un <see cref="CompletionItem"/> ;
    /// on l'envoie en <c>CommandParameter</c>.
    /// </summary>
    private void CompletionList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox lb) return;
        if (lb.SelectedItem is not CompletionItem item) return;
        if (DataContext is not EditerRubriqueViewModel vm) return;
        if (vm.InsererCompletionCommand.CanExecute(item))
            vm.InsererCompletionCommand.Execute(item);
    }
}
