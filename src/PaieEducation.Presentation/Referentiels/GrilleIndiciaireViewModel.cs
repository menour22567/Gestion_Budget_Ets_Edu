using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Application.Referentiels.UseCases;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Calcul.ValueObjects;
using PaieEducation.Presentation.Dialogs;

namespace PaieEducation.Presentation.Referentiels;

/// <summary>
/// Écran « Grille indiciaire » (Phase 6, tâche 3 — paramétrage des
/// référentiels, Q3) : 4 sous-formulaires indépendants, un par use case
/// déjà livré en Phase 5 (<see cref="DefinirValeurPoint"/>,
/// <see cref="DefinirIndiceMinGrille"/>, <see cref="DefinirIndiceEchelon"/>,
/// <see cref="DupliquerVersion"/>) — regroupés dans un seul écran car ils
/// partagent le même repository (<c>IGrilleIndiciaireRepository</c>) et le
/// même concept métier. Catégorie/Échelon sont sélectionnés via
/// <see cref="ListerReferentiels"/> (chargé au montage, cf.
/// <see cref="ChargerReferentielsCommand"/>) — <c>ValeurPoint</c> n'a pas
/// de clé référentielle, l'onglet « Valeur du point » reste en saisie libre.
/// </summary>
public sealed partial class GrilleIndiciaireViewModel : ObservableObject
{
    private readonly DefinirValeurPoint _definirValeurPoint;
    private readonly DefinirIndiceMinGrille _definirIndiceMinGrille;
    private readonly DefinirIndiceEchelon _definirIndiceEchelon;
    private readonly DupliquerVersion _dupliquerVersion;
    private readonly ListerReferentiels _listerReferentiels;
    private readonly IDialogService _dialogs;

    public ObservableCollection<ReferentielItem> CategoriesDisponibles { get; } = [];
    public ObservableCollection<ReferentielItem> EchelonsDisponibles { get; } = [];

    // -- Valeur du point indiciaire --
    [ObservableProperty] private string vpValeur = string.Empty;
    [ObservableProperty] private string vpDateEffet = string.Empty;
    [ObservableProperty] private string vpVersion = string.Empty;
    [ObservableProperty] private string? vpResultat;
    [ObservableProperty] private bool vpEnCours;

    // -- Indice minimum de grille (par catégorie) --
    [ObservableProperty] private ReferentielItem? imCategorieSelectionnee;
    [ObservableProperty] private string imIndiceMin = string.Empty;
    [ObservableProperty] private string imDateEffet = string.Empty;
    [ObservableProperty] private string imVersion = string.Empty;
    [ObservableProperty] private string? imResultat;
    [ObservableProperty] private bool imEnCours;

    // -- Indice d'échelon --
    [ObservableProperty] private ReferentielItem? ieEchelonSelectionne;
    [ObservableProperty] private string ieIndice = string.Empty;
    [ObservableProperty] private string ieDateEffet = string.Empty;
    [ObservableProperty] private string ieVersion = string.Empty;
    [ObservableProperty] private string? ieResultat;
    [ObservableProperty] private bool ieEnCours;

    // -- Dupliquer la valeur du point (mode « Duplication », J3I §7.4) --
    [ObservableProperty] private string dvDateEffet = string.Empty;
    [ObservableProperty] private string dvVersion = string.Empty;
    [ObservableProperty] private string? dvSource;
    [ObservableProperty] private string? dvResultat;
    [ObservableProperty] private bool dvEnCours;

    public GrilleIndiciaireViewModel(
        DefinirValeurPoint definirValeurPoint, DefinirIndiceMinGrille definirIndiceMinGrille,
        DefinirIndiceEchelon definirIndiceEchelon, DupliquerVersion dupliquerVersion,
        ListerReferentiels listerReferentiels, IDialogService dialogs)
    {
        _definirValeurPoint = definirValeurPoint ?? throw new ArgumentNullException(nameof(definirValeurPoint));
        _definirIndiceMinGrille = definirIndiceMinGrille ?? throw new ArgumentNullException(nameof(definirIndiceMinGrille));
        _definirIndiceEchelon = definirIndiceEchelon ?? throw new ArgumentNullException(nameof(definirIndiceEchelon));
        _dupliquerVersion = dupliquerVersion ?? throw new ArgumentNullException(nameof(dupliquerVersion));
        _listerReferentiels = listerReferentiels ?? throw new ArgumentNullException(nameof(listerReferentiels));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        _ = ChargerReferentielsCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task ChargerReferentielsAsync()
    {
        var result = await _listerReferentiels.ExecuterAsync();
        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync(result.Error.Message);
            return;
        }

        CategoriesDisponibles.Clear();
        foreach (var c in result.Value.Categories) CategoriesDisponibles.Add(c);
        EchelonsDisponibles.Clear();
        foreach (var e in result.Value.Echelons) EchelonsDisponibles.Add(e);
    }

    [RelayCommand]
    private async Task DefinirValeurPointAsync()
    {
        if (!decimal.TryParse(VpValeur, NumberStyles.Number, CultureInfo.InvariantCulture, out var valeur))
        {
            await _dialogs.ShowErrorAsync($"Valeur invalide : « {VpValeur} ».");
            return;
        }

        VpEnCours = true;
        VpResultat = null;
        try
        {
            var result = await _definirValeurPoint.ExecuterAsync(new DefinirValeurPoint.Demande(valeur, VpDateEffet, VpVersion));
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }
            VpResultat = $"Valeur du point définie (Id : {result.Value})";
        }
        finally
        {
            VpEnCours = false;
        }
    }

    [RelayCommand]
    private async Task DefinirIndiceMinAsync()
    {
        if (!int.TryParse(ImIndiceMin, NumberStyles.Integer, CultureInfo.InvariantCulture, out var indiceMin))
        {
            await _dialogs.ShowErrorAsync($"Indice minimum invalide : « {ImIndiceMin} ».");
            return;
        }

        ImEnCours = true;
        ImResultat = null;
        try
        {
            var result = await _definirIndiceMinGrille.ExecuterAsync(
                new DefinirIndiceMinGrille.Demande(ImCategorieSelectionnee?.Id ?? string.Empty, indiceMin, ImDateEffet, ImVersion));
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }
            ImResultat = $"Indice minimum défini (Id : {result.Value})";
        }
        finally
        {
            ImEnCours = false;
        }
    }

    [RelayCommand]
    private async Task DefinirIndiceEchelonAsync()
    {
        if (!int.TryParse(IeIndice, NumberStyles.Integer, CultureInfo.InvariantCulture, out var indice))
        {
            await _dialogs.ShowErrorAsync($"Indice invalide : « {IeIndice} ».");
            return;
        }

        IeEnCours = true;
        IeResultat = null;
        try
        {
            var result = await _definirIndiceEchelon.ExecuterAsync(
                new DefinirIndiceEchelon.Demande(IeEchelonSelectionne?.Id ?? string.Empty, indice, IeDateEffet, IeVersion));
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }
            IeResultat = $"Indice d'échelon défini (Id : {result.Value})";
        }
        finally
        {
            IeEnCours = false;
        }
    }

    [RelayCommand]
    private async Task DupliquerValeurPointAsync()
    {
        DvEnCours = true;
        DvResultat = null;
        try
        {
            var result = await _dupliquerVersion.ExecuterAsync(new DupliquerVersion.Demande(DvDateEffet, DvVersion, DvSource));
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }
            DvResultat = $"Valeur du point dupliquée (Id : {result.Value})";
        }
        finally
        {
            DvEnCours = false;
        }
    }
}
