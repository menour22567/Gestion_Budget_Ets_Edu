using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Presentation.Dialogs;

namespace PaieEducation.Presentation.Workbench;

/// <summary>
/// Écran « Fiche rubrique » (Phase 6, tâche 4, catalogue Rubriques) —
/// consultation en lecture seule (Identité/Barème/Éligibilité) via
/// <see cref="ConsulterFicheRubrique"/>. Couvre de facto l'item « IFC
/// (P12) » de la tâche 4 : IFC n'est pas une entité à part, seulement une
/// rubrique parmi d'autres consultable ici (barème par catégorie).
/// </summary>
/// <remarks>
/// Onglets « Formule » et édition (Barème/Éligibilité en écriture)
/// restent hors périmètre — aucun chemin d'écriture n'existe pour les
/// barèmes/conditions ISSRP (tâches 5-7). Saisie de <see cref="RubriqueId"/>
/// en texte libre (pas de sélecteur <c>ComboBox</c>) — construire un use
/// case de liste des rubriques pour un sélecteur n'était pas dans le
/// périmètre de cette tranche.
/// </remarks>
public sealed partial class FicheRubriqueViewModel : ObservableObject
{
    private readonly ConsulterFicheRubrique _consulterFicheRubrique;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private string rubriqueId = string.Empty;

    [ObservableProperty]
    private string datePaie = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");

    [ObservableProperty]
    private RubriqueDetail? detail;

    [ObservableProperty]
    private bool enCours;

    public ObservableCollection<BaremeValue> Baremes { get; } = [];
    public ObservableCollection<ConditionEligibilite> Conditions { get; } = [];

    public FicheRubriqueViewModel(ConsulterFicheRubrique consulterFicheRubrique, IDialogService dialogs)
    {
        _consulterFicheRubrique = consulterFicheRubrique ?? throw new ArgumentNullException(nameof(consulterFicheRubrique));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    [RelayCommand]
    private async Task ChargerAsync()
    {
        EnCours = true;
        try
        {
            var result = await _consulterFicheRubrique.ExecuterAsync(
                new ConsulterFicheRubrique.Demande(RubriqueId, DatePaie));
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }

            Detail = result.Value.Detail;
            Baremes.Clear();
            foreach (var bareme in result.Value.Baremes)
                Baremes.Add(bareme);
            Conditions.Clear();
            foreach (var condition in result.Value.Conditions)
                Conditions.Add(condition);
        }
        finally
        {
            EnCours = false;
        }
    }
}
