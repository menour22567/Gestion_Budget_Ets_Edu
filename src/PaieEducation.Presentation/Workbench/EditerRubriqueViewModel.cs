using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Application.Referentiels.UseCases;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Calcul.Formules;
using PaieEducation.Domain.Workbench.Constants;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Presentation.Navigation;

namespace PaieEducation.Presentation.Workbench;

/// <summary>
/// Écran « Éditer une rubrique » (chantier C4.1 — écriture des rubriques &amp;
/// formules) : création/édition de l'identité d'une rubrique, définition d'une
/// version de formule (validée par le <see cref="FormulaParser"/> avant
/// soumission) et définition d'un paramètre versionné. Symétrique en écriture
/// des lectures du moteur, côté UI.
/// </summary>
/// <remarks>
/// Aucune logique de parsing ne fuit en exception : la formule est validée
/// localement (message clair) avant appel au use case ; les échecs applicatifs
/// sont présentés via <see cref="IDialogService"/>.
/// </remarks>
public sealed partial class EditerRubriqueViewModel : ObservableObject
{
    private readonly DefinirRubrique _definirRubrique;
    private readonly DefinirFormuleRubrique _definirFormule;
    private readonly DefinirParametreRubrique _definirParametre;
    private readonly DefinirValeurBareme _definirBareme;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;

    public ObservableCollection<string> Natures { get; } = ["GAIN", "RETENUE", "COTISATION", "IMPOT"];
    public ObservableCollection<string> BasesCalcul { get; } = ["TRAITEMENT", "TBASE", "TBASE_ECHELON", "INDICE_ECHELON", "FORFAIT", "ASSIETTE_COTISABLE", "ASSIETTE_IMPOSABLE"];
    public ObservableCollection<string> Periodicites { get; } = ["MENSUELLE", "TRIMESTRIELLE", "ANNUELLE", "PONCTUELLE"];

    // -- Identité de la rubrique --
    [ObservableProperty] private string rubriqueId = string.Empty;
    [ObservableProperty] private string libelle = string.Empty;
    [ObservableProperty] private string nature = "GAIN";
    [ObservableProperty] private string baseCalcul = "TRAITEMENT";
    [ObservableProperty] private string periodicite = "MENSUELLE";
    [ObservableProperty] private string periodiciteVersement = string.Empty;
    [ObservableProperty] private string ordreCalcul = "10";
    [ObservableProperty] private bool estImposable = true;
    [ObservableProperty] private bool estCotisable = true;
    [ObservableProperty] private string description = string.Empty;
    [ObservableProperty] private bool estAffectableManuellement = true;
    [ObservableProperty] private bool occurrencesMultiples;
    [ObservableProperty] private string? sourceValeurId = string.Empty;
    [ObservableProperty] private string? source = string.Empty;
    [ObservableProperty] private bool identiteEnCours;
    [ObservableProperty] private string? identiteResultat;

    // -- Formule versionnée --
    [ObservableProperty] private string formuleRubriqueId = string.Empty;
    [ObservableProperty] private string formuleExpression = string.Empty;
    [ObservableProperty] private string formuleDateEffet = string.Empty;
    [ObservableProperty] private string formuleValidation = string.Empty;
    [ObservableProperty] private bool formuleEnCours;
    [ObservableProperty] private string? formuleResultat;

    // -- Paramètre versionné --
    [ObservableProperty] private string parametreRubriqueId = string.Empty;
    [ObservableProperty] private string parametreCle = string.Empty;
    [ObservableProperty] private string parametreValeur = string.Empty;
    [ObservableProperty] private string parametreDateEffet = string.Empty;
    [ObservableProperty] private string? parametreSource = string.Empty;
    [ObservableProperty] private bool parametreEnCours;
    [ObservableProperty] private string? parametreResultat;

    // -- Barème versionné (chantier P5, audit du 19/07/2026) --
    public ObservableCollection<string> BaremeDimensions { get; } = [.. BaremeDimensionKeys.ValidesPourRubriqueBaremes];
    public ObservableCollection<string> BaremeTypesValeur { get; } = [.. BaremeTypeValeurKeys.Valides];

    [ObservableProperty] private string baremeRubriqueId = string.Empty;
    [ObservableProperty] private string baremeDimension = BaremeDimensionKeys.Categorie;
    [ObservableProperty] private string baremeBorneInf = string.Empty;
    [ObservableProperty] private string? baremeBorneSup = string.Empty;
    [ObservableProperty] private string baremeTypeValeur = BaremeTypeValeurKeys.Taux;
    [ObservableProperty] private string baremeValeur = string.Empty;
    [ObservableProperty] private string baremeDateEffet = string.Empty;
    [ObservableProperty] private string? baremeSource = string.Empty;
    [ObservableProperty] private bool baremeEnCours;
    [ObservableProperty] private string? baremeResultat;

    public EditerRubriqueViewModel(
        DefinirRubrique definirRubrique, DefinirFormuleRubrique definirFormule,
        DefinirParametreRubrique definirParametre, DefinirValeurBareme definirBareme,
        IDialogService dialogs, INavigationService navigation)
    {
        _definirRubrique = definirRubrique ?? throw new ArgumentNullException(nameof(definirRubrique));
        _definirFormule = definirFormule ?? throw new ArgumentNullException(nameof(definirFormule));
        _definirParametre = definirParametre ?? throw new ArgumentNullException(nameof(definirParametre));
        _definirBareme = definirBareme ?? throw new ArgumentNullException(nameof(definirBareme));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
    }

    [RelayCommand]
    private void ValiderFormule()
    {
        if (string.IsNullOrWhiteSpace(FormuleExpression))
        {
            FormuleValidation = "Saisissez une expression.";
            return;
        }

        var parse = FormulaParser.Parser(FormuleExpression);
        FormuleValidation = parse.IsSuccess
            ? "Formule valide."
            : $"Formule invalide : {parse.Error.Message}";
    }

    [RelayCommand]
    private async Task DefinirIdentiteAsync()
    {
        if (!int.TryParse(OrdreCalcul, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ordre) || ordre < 0)
        {
            await _dialogs.ShowErrorAsync($"Ordre de calcul invalide : « {OrdreCalcul} ».");
            return;
        }

        IdentiteEnCours = true;
        IdentiteResultat = null;
        try
        {
            var result = await _definirRubrique.ExecuterAsync(new DefinirRubrique.Demande(
                RubriqueId, Libelle, Nature, BaseCalcul, Periodicite,
                string.IsNullOrWhiteSpace(PeriodiciteVersement) ? null : PeriodiciteVersement,
                ordre, EstImposable, EstCotisable, Description, EstAffectableManuellement,
                OccurrencesMultiples,
                string.IsNullOrWhiteSpace(SourceValeurId) ? null : SourceValeurId,
                string.IsNullOrWhiteSpace(Source) ? null : Source));
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }
            IdentiteResultat = $"Rubrique enregistrée (Id : {result.Value})";
            _navigation.NavigateTo<FicheRubriqueViewModel>();
        }
        finally
        {
            IdentiteEnCours = false;
        }
    }

    [RelayCommand]
    private async Task DefinirFormuleAsync()
    {
        if (string.IsNullOrWhiteSpace(FormuleRubriqueId))
        {
            await _dialogs.ShowErrorAsync("Identifiant de la rubrique requis.");
            return;
        }

        var parse = FormulaParser.Parser(FormuleExpression);
        if (parse.IsFailure)
        {
            await _dialogs.ShowErrorAsync($"Formule invalide : {parse.Error.Message}");
            return;
        }

        FormuleEnCours = true;
        FormuleResultat = null;
        try
        {
            var result = await _definirFormule.ExecuterAsync(new DefinirFormuleRubrique.Demande(
                FormuleRubriqueId, FormuleExpression, FormuleDateEffet));
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }
            FormuleResultat = $"Formule enregistrée (Id : {result.Value})";
        }
        finally
        {
            FormuleEnCours = false;
        }
    }

    [RelayCommand]
    private async Task DefinirParametreAsync()
    {
        if (string.IsNullOrWhiteSpace(ParametreRubriqueId) || string.IsNullOrWhiteSpace(ParametreCle))
        {
            await _dialogs.ShowErrorAsync("Rubrique et clé de paramètre requises.");
            return;
        }

        ParametreEnCours = true;
        ParametreResultat = null;
        try
        {
            var result = await _definirParametre.ExecuterAsync(new DefinirParametreRubrique.Demande(
                ParametreRubriqueId, ParametreCle, ParametreValeur, ParametreDateEffet,
                string.IsNullOrWhiteSpace(ParametreSource) ? null : ParametreSource));
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }
            ParametreResultat = $"Paramètre enregistré (Id : {result.Value})";
        }
        finally
        {
            ParametreEnCours = false;
        }
    }

    [RelayCommand]
    private async Task DefinirBaremeAsync()
    {
        if (string.IsNullOrWhiteSpace(BaremeRubriqueId) || string.IsNullOrWhiteSpace(BaremeBorneInf))
        {
            await _dialogs.ShowErrorAsync("Rubrique et borne inférieure de tranche requises.");
            return;
        }

        BaremeEnCours = true;
        BaremeResultat = null;
        try
        {
            var result = await _definirBareme.ExecuterAsync(new DefinirValeurBareme.Demande(
                BaremeRubriqueId, BaremeDimension, BaremeBorneInf,
                string.IsNullOrWhiteSpace(BaremeBorneSup) ? null : BaremeBorneSup,
                BaremeTypeValeur, BaremeValeur, BaremeDateEffet,
                string.IsNullOrWhiteSpace(BaremeSource) ? null : BaremeSource));
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }
            BaremeResultat = $"Barème enregistré (Id : {result.Value})";
        }
        finally
        {
            BaremeEnCours = false;
        }
    }
}
