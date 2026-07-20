using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Workbench.Constants;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Presentation.Dialogs;

namespace PaieEducation.Presentation.Workbench;

/// <summary>
/// Écran « Fiche rubrique » (Phase 6, tâche 4, catalogue Rubriques) —
/// consultation (Identité/Barème/Éligibilité) via
/// <see cref="ConsulterFicheRubrique"/>, complétée par l'édition DNF
/// (chantier P6, audit du 19/07/2026 : <see cref="DefinirGroupeEligibilite"/>,
/// <see cref="CloreGroupeEligibilite"/>, <see cref="DefinirRegleEligibilite"/>,
/// <see cref="CloreRegleEligibilite"/>). Couvre de facto l'item « IFC
/// (P12) » de la tâche 4 : IFC n'est pas une entité à part, seulement une
/// rubrique parmi d'autres consultable ici (barème par catégorie).
/// </summary>
/// <remarks>
/// Saisie de <see cref="RubriqueId"/> en texte libre (pas de sélecteur
/// <c>ComboBox</c>) — même convention que les autres onglets d'édition
/// (<c>EditerRubriqueViewModel</c>). <see cref="Criteres"/> est en revanche
/// chargé depuis <c>CriteresEligibilite</c> (zéro saisie libre pour ce
/// champ — convention explicite du plan P6). <see cref="Severites"/>/
/// <see cref="Operateurs"/> sont les valeurs CHECK figées du schéma, comme
/// <c>BaremeDimensions</c> sur l'onglet Barème.
/// </remarks>
public sealed partial class FicheRubriqueViewModel : ObservableObject
{
    // Acteur déclaratif — aucun mécanisme d'identité utilisateur en V1
    // (dette documentée, décision P17/Q12 en attente).
    private const string ActeurWorkbench = "workbench";

    private readonly ConsulterFicheRubrique _consulterFicheRubrique;
    private readonly DefinirGroupeEligibilite _definirGroupe;
    private readonly CloreGroupeEligibilite _cloreGroupe;
    private readonly DefinirRegleEligibilite _definirRegle;
    private readonly CloreRegleEligibilite _cloreRegle;
    private readonly ListerCriteresEligibilite _listerCriteres;
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
    public ObservableCollection<GroupeEligibilite> Groupes { get; } = [];
    public ObservableCollection<CritereEligibilite> Criteres { get; } = [];
    public ObservableCollection<string> Severites { get; } = [.. SeveriteKeys.Valides];
    public ObservableCollection<string> Operateurs { get; } = [.. OperateurKeys.Valides];

    // -- Nouveau groupe DNF --
    [ObservableProperty] private string nouveauGroupeId = string.Empty;
    [ObservableProperty] private string nouveauGroupeRubriqueId = string.Empty;
    [ObservableProperty] private string nouveauGroupeSeverite = SeveriteKeys.Info;
    [ObservableProperty] private string? nouveauGroupeMessageId = string.Empty;
    [ObservableProperty] private string nouveauGroupePriorite = "100";
    [ObservableProperty] private string nouveauGroupeDateEffet = string.Empty;
    [ObservableProperty] private string? nouveauGroupeSource = string.Empty;
    [ObservableProperty] private bool groupeEnCours;
    [ObservableProperty] private string? groupeResultat;

    // -- Clôture de groupe --
    [ObservableProperty] private string groupeACloturerId = string.Empty;
    [ObservableProperty] private string groupeClotureDateFin = string.Empty;

    // -- Nouvelle condition d'éligibilité --
    [ObservableProperty] private string nouvelleRegleRubriqueId = string.Empty;
    [ObservableProperty] private CritereEligibilite? nouvelleRegleCritere;
    [ObservableProperty] private string? nouvelleRegleGroupeId = string.Empty;
    [ObservableProperty] private string nouvelleRegleOperateur = OperateurKeys.Egal;
    [ObservableProperty] private string nouvelleRegleValeur = string.Empty;
    [ObservableProperty] private string nouvelleRegleDateEffet = string.Empty;
    [ObservableProperty] private string? nouvelleRegleSource = string.Empty;
    [ObservableProperty] private bool regleEnCours;
    [ObservableProperty] private string? regleResultat;

    // -- Clôture de condition --
    [ObservableProperty] private string regleACloturerId = string.Empty;
    [ObservableProperty] private string regleClotureDateFin = string.Empty;

    public FicheRubriqueViewModel(
        ConsulterFicheRubrique consulterFicheRubrique,
        DefinirGroupeEligibilite definirGroupe,
        CloreGroupeEligibilite cloreGroupe,
        DefinirRegleEligibilite definirRegle,
        CloreRegleEligibilite cloreRegle,
        ListerCriteresEligibilite listerCriteres,
        IDialogService dialogs)
    {
        _consulterFicheRubrique = consulterFicheRubrique ?? throw new ArgumentNullException(nameof(consulterFicheRubrique));
        _definirGroupe = definirGroupe ?? throw new ArgumentNullException(nameof(definirGroupe));
        _cloreGroupe = cloreGroupe ?? throw new ArgumentNullException(nameof(cloreGroupe));
        _definirRegle = definirRegle ?? throw new ArgumentNullException(nameof(definirRegle));
        _cloreRegle = cloreRegle ?? throw new ArgumentNullException(nameof(cloreRegle));
        _listerCriteres = listerCriteres ?? throw new ArgumentNullException(nameof(listerCriteres));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        _ = ChargerCriteresCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task ChargerCriteresAsync()
    {
        var result = await _listerCriteres.ExecuterAsync();
        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync(result.Error.Message);
            return;
        }

        Criteres.Clear();
        foreach (var critere in result.Value)
            Criteres.Add(critere);
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
            Groupes.Clear();
            foreach (var groupe in result.Value.Groupes)
                Groupes.Add(groupe);
        }
        finally
        {
            EnCours = false;
        }
    }

    [RelayCommand]
    private async Task DefinirGroupeAsync()
    {
        if (string.IsNullOrWhiteSpace(NouveauGroupeId) || string.IsNullOrWhiteSpace(NouveauGroupeRubriqueId))
        {
            await _dialogs.ShowErrorAsync("Identifiant de groupe et rubrique requis.");
            return;
        }
        if (!int.TryParse(NouveauGroupePriorite, out var priorite) || priorite < 0)
        {
            await _dialogs.ShowErrorAsync($"Priorité invalide : « {NouveauGroupePriorite} ».");
            return;
        }

        GroupeEnCours = true;
        GroupeResultat = null;
        try
        {
            var result = await _definirGroupe.ExecuterAsync(new DefinirGroupeEligibilite.Demande(
                NouveauGroupeId, NouveauGroupeRubriqueId, NouveauGroupeSeverite,
                string.IsNullOrWhiteSpace(NouveauGroupeMessageId) ? null : NouveauGroupeMessageId,
                priorite, NouveauGroupeDateEffet, null,
                string.IsNullOrWhiteSpace(NouveauGroupeSource) ? null : NouveauGroupeSource,
                ActeurWorkbench));
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }
            GroupeResultat = $"Groupe DNF enregistré (Id : {result.Value})";
        }
        finally
        {
            GroupeEnCours = false;
        }
    }

    [RelayCommand]
    private async Task CloreGroupeAsync()
    {
        if (string.IsNullOrWhiteSpace(GroupeACloturerId) || string.IsNullOrWhiteSpace(GroupeClotureDateFin))
        {
            await _dialogs.ShowErrorAsync("Identifiant de groupe et date de fin requis.");
            return;
        }

        GroupeEnCours = true;
        GroupeResultat = null;
        try
        {
            var result = await _cloreGroupe.ExecuterAsync(
                new CloreGroupeEligibilite.Demande(GroupeACloturerId, GroupeClotureDateFin));
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }
            GroupeResultat = $"Groupe DNF clos (Id : {result.Value})";
        }
        finally
        {
            GroupeEnCours = false;
        }
    }

    [RelayCommand]
    private async Task DefinirRegleAsync()
    {
        if (string.IsNullOrWhiteSpace(NouvelleRegleRubriqueId) || NouvelleRegleCritere is null)
        {
            await _dialogs.ShowErrorAsync("Rubrique et critère requis.");
            return;
        }

        RegleEnCours = true;
        RegleResultat = null;
        try
        {
            var result = await _definirRegle.ExecuterAsync(new DefinirRegleEligibilite.Demande(
                NouvelleRegleRubriqueId, NouvelleRegleCritere.Id,
                string.IsNullOrWhiteSpace(NouvelleRegleGroupeId) ? null : NouvelleRegleGroupeId,
                NouvelleRegleOperateur, NouvelleRegleValeur, NouvelleRegleDateEffet,
                string.IsNullOrWhiteSpace(NouvelleRegleSource) ? null : NouvelleRegleSource));
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }
            RegleResultat = $"Condition enregistrée (Id : {result.Value})";
        }
        finally
        {
            RegleEnCours = false;
        }
    }

    [RelayCommand]
    private async Task CloreRegleAsync()
    {
        if (string.IsNullOrWhiteSpace(RegleACloturerId) || string.IsNullOrWhiteSpace(RegleClotureDateFin))
        {
            await _dialogs.ShowErrorAsync("Identifiant de condition et date de fin requis.");
            return;
        }

        RegleEnCours = true;
        RegleResultat = null;
        try
        {
            var result = await _cloreRegle.ExecuterAsync(
                new CloreRegleEligibilite.Demande(RegleACloturerId, RegleClotureDateFin));
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }
            RegleResultat = $"Condition close (Id : {result.Value})";
        }
        finally
        {
            RegleEnCours = false;
        }
    }
}
