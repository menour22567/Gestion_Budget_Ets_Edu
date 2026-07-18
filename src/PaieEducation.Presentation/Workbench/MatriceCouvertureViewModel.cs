using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Presentation.Dialogs;

namespace PaieEducation.Presentation.Workbench;

/// <summary>
/// Écran « Matrice de couverture » (Phase 6, tâche 9, D11) — liste plate
/// des cellules (Corps × Rubrique) produites par
/// <see cref="ListerMatriceCouverture"/> (déjà livré, Phase 5). Rendu en
/// <c>DataGrid</c> à colonnes statiques (tri natif par en-tête) plutôt
/// qu'en grille pivotée visuelle — décision explicite (voir mémoire
/// phase6-matrice-couverture-ecran) : pas de code couleur (la 4e nuance
/// « Gris » du mockup J3I §5.5 reste non définie côté backend).
/// </summary>
public sealed partial class MatriceCouvertureViewModel : ObservableObject
{
    private readonly ListerMatriceCouverture _listerMatriceCouverture;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private string datePaie = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");

    [ObservableProperty]
    private bool enCours;

    public ObservableCollection<CelluleCouverture> Cellules { get; } = [];

    public MatriceCouvertureViewModel(ListerMatriceCouverture listerMatriceCouverture, IDialogService dialogs)
    {
        _listerMatriceCouverture = listerMatriceCouverture ?? throw new ArgumentNullException(nameof(listerMatriceCouverture));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    [RelayCommand]
    private async Task ChargerAsync()
    {
        EnCours = true;
        try
        {
            var result = await _listerMatriceCouverture.ExecuterAsync(new ListerMatriceCouverture.Demande(DatePaie));
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }

            Cellules.Clear();
            foreach (var cellule in result.Value)
                Cellules.Add(cellule);
        }
        finally
        {
            EnCours = false;
        }
    }
}
