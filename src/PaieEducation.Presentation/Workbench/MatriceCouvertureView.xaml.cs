using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace PaieEducation.Presentation.Workbench;

/// <summary>
/// Génère les colonnes de la matrice pivotée en code-behind (P7) : une colonne
/// « Corps » fixe, puis une colonne par rubrique de
/// <see cref="MatriceCouvertureViewModel.Colonnes"/> — WPF ne permet pas de
/// binder un nombre de colonnes variable directement en XAML. Chaque cellule
/// est un bouton coloré (état) cliquable (drill-down vers la fiche rubrique).
/// </summary>
public partial class MatriceCouvertureView : UserControl
{
    public MatriceCouvertureView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MatriceCouvertureViewModel ancien)
            ancien.Colonnes.CollectionChanged -= OnColonnesChanged;

        if (e.NewValue is MatriceCouvertureViewModel nouveau)
        {
            nouveau.Colonnes.CollectionChanged += OnColonnesChanged;
            ReconstruireColonnes(nouveau.Colonnes);
        }
    }

    private void OnColonnesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => ReconstruireColonnes((IEnumerable<string>)sender!);

    private void ReconstruireColonnes(IEnumerable<string> rubriqueIds)
    {
        Grille.Columns.Clear();
        Grille.Columns.Add(new DataGridTextColumn
        {
            Header = "Corps",
            Binding = new Binding(nameof(LigneMatriceCorps.CorpsId)),
            Width = 160,
        });

        foreach (var rubriqueId in rubriqueIds)
            Grille.Columns.Add(ConstruireColonneRubrique(rubriqueId));
    }

    private static DataGridTemplateColumn ConstruireColonneRubrique(string rubriqueId)
    {
        var bouton = new FrameworkElementFactory(typeof(Button));
        bouton.SetValue(Control.BorderThicknessProperty, new Thickness(0));
        bouton.SetValue(Control.PaddingProperty, new Thickness(4, 2, 4, 2));
        bouton.SetValue(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center);
        bouton.SetValue(Control.FontWeightProperty, FontWeights.Bold);
        bouton.SetValue(Control.ForegroundProperty, System.Windows.Media.Brushes.White);
        bouton.SetValue(FrameworkElement.MarginProperty, new Thickness(1));

        var cheminEtat = $"EtatsParRubrique[{rubriqueId}]";
        bouton.SetBinding(ContentControl.ContentProperty,
            new Binding(cheminEtat) { Converter = EtatCouvertureVersSymboleConverter.Instance });
        bouton.SetBinding(Control.BackgroundProperty,
            new Binding(cheminEtat) { Converter = EtatCouvertureVersBrushConverter.Instance });
        bouton.SetBinding(Button.CommandProperty,
            new Binding($"DataContext.{nameof(MatriceCouvertureViewModel.NaviguerVersFicheCommand)}")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UserControl), 1),
            });
        bouton.SetValue(Button.CommandParameterProperty, rubriqueId);

        return new DataGridTemplateColumn
        {
            Header = rubriqueId,
            Width = 90,
            CellTemplate = new DataTemplate { VisualTree = bouton },
        };
    }
}
