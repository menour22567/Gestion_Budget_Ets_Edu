using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Application.Referentiels.UseCases;
using PaieEducation.Application.Workbench.Services;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Agents.Repositories;
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
/// <para><b>P10 (FormulaEditor avancé, 22/07/2026)</b> : la validation de la
/// formule passe en <b>live</b> (déclenchée par <c>OnFormuleExpressionChanged</c>)
/// et un <b>popup d'auto-complétion</b> apparaît sur demande
/// (<c>DemanderCompletion</c>) avec un catalogue de fonctions, variables,
/// sources de valeur et rubriques. Un panneau <b>simulation agent témoin</b>
/// permet de relancer le pipeline avec la formule en cours sur un agent réel
/// et d'afficher le delta de net (le « vrai filet » de P10).</para>
/// <para>Aucune logique de parsing ne fuit en exception : la formule est validée
/// localement (message clair) avant appel au use case ; les échecs applicatifs
/// sont présentés via <see cref="IDialogService"/>.</para>
/// </remarks>
public sealed partial class EditerRubriqueViewModel : ObservableObject
{
    private readonly DefinirRubrique _definirRubrique;
    private readonly DefinirFormuleRubrique _definirFormule;
    private readonly DefinirParametreRubrique _definirParametre;
    private readonly DefinirValeurBareme _definirBareme;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly IFormuleCompletionProvider? _completion;
    private readonly IAgentReadRepository? _agents;
    private readonly SimulerBulletinPourFormule? _simulateur;

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
    [ObservableProperty] private bool formuleValidationEstValide;
    [ObservableProperty] private int? formuleValidationNbNoeuds;
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

    // -- P10 : auto-complétion + simulation agent témoin --
    [ObservableProperty] private string completionPrefixe = string.Empty;
    [ObservableProperty] private bool completionPopupOuvert;
    public ObservableCollection<CompletionItem> CompletionItems { get; } = new();
    public ObservableCollection<AgentResume> AgentsTemoins { get; } = new();
    [ObservableProperty] private AgentResume? agentTemoinSelectionne;
    [ObservableProperty] private string datePaieSimulation = "2025-06-01";
    [ObservableProperty] private bool simulationEnCours;
    [ObservableProperty] private string? resultatSimulation;
    [ObservableProperty] private decimal? netBaseline;
    [ObservableProperty] private decimal? netOverride;
    [ObservableProperty] private decimal? deltaNet;

    /// <summary>
    /// Constructeur nominal (DI) : reçoit tous les use cases et services
    /// P10. C'est le seul constructeur exposé en production. Les services
    /// P10 (completion, agents, simulateur) sont Nullable<T> en interne :
    /// le constructeur historique (6 args) leur passe <c>null</c>
    /// intentionnellement pour conserver la compat des tests C4.1
    /// pré-P10 ; en production (DI), ils sont toujours non nuls.
    /// </summary>
    public EditerRubriqueViewModel(
        DefinirRubrique definirRubrique, DefinirFormuleRubrique definirFormule,
        DefinirParametreRubrique definirParametre, DefinirValeurBareme definirBareme,
        IDialogService dialogs, INavigationService navigation,
        IFormuleCompletionProvider completion,
        IAgentReadRepository agents,
        SimulerBulletinPourFormule simulateur)
    {
        _definirRubrique = definirRubrique ?? throw new ArgumentNullException(nameof(definirRubrique));
        _definirFormule = definirFormule ?? throw new ArgumentNullException(nameof(definirFormule));
        _definirParametre = definirParametre ?? throw new ArgumentNullException(nameof(definirParametre));
        _definirBareme = definirBareme ?? throw new ArgumentNullException(nameof(definirBareme));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _completion = completion; // Nullable : permet le fallback historique.
        _agents = agents;
        _simulateur = simulateur;
    }

    /// <summary>
    /// Constructeur historique conservé pour les tests existants (C4.1
    /// d'origine, avant P10). Les features P10 sont désactivées
    /// (completion / simulation == null).
    /// </summary>
    public EditerRubriqueViewModel(
        DefinirRubrique definirRubrique, DefinirFormuleRubrique definirFormule,
        DefinirParametreRubrique definirParametre, DefinirValeurBareme definirBareme,
        IDialogService dialogs, INavigationService navigation)
        : this(definirRubrique, definirFormule, definirParametre, definirBareme, dialogs, navigation,
               completion: null!, agents: null!, simulateur: null!)
    {
    }

    [RelayCommand]
    private void ValiderFormule()
    {
        if (string.IsNullOrWhiteSpace(FormuleExpression))
        {
            FormuleValidation = "Saisissez une expression.";
            FormuleValidationEstValide = false;
            FormuleValidationNbNoeuds = null;
            return;
        }
        ValiderFormuleInterne(FormuleExpression);
    }

    /// <summary>
    /// P10 : validation live de la formule. Déclenchée automatiquement à
    /// chaque modification de <see cref="FormuleExpression"/> par le source
    /// generator <c>[ObservableProperty]</c>. Pas de debounce : le
    /// <see cref="FormulaParser"/> est suffisamment rapide pour ne pas
    /// dégrader la frappe. Le compteur de nœuds de l'AST est exposé
    /// séparément pour permettre à l'UI d'afficher un retour plus fin
    /// (couleur de bordure, libellé "N nœuds").
    /// </summary>
    partial void OnFormuleExpressionChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            FormuleValidation = string.Empty;
            FormuleValidationEstValide = false;
            FormuleValidationNbNoeuds = null;
            CompletionPrefixe = string.Empty;
            CompletionItems.Clear();
            CompletionPopupOuvert = false;
            return;
        }
        ValiderFormuleInterne(value);

        // P10 — auto-complétion : on extrait le dernier mot en cours de frappe
        // (séquence [A-Za-z_][A-Za-z0-9_]*) à la fin du texte et on demande
        // les suggestions au provider. Si le provider n'est pas câblé (mode
        // test legacy) ou si le prefixe est vide, on ferme le popup.
        var prefixe = ExtrairePrefixeCourant(value);
        CompletionPrefixe = prefixe;
        if (_completion is null || prefixe.Length == 0)
        {
            CompletionItems.Clear();
            CompletionPopupOuvert = false;
            return;
        }
        _ = DemanderCompletionAsync();
    }

    /// <summary>
    /// P10 — extrait le dernier mot en cours de frappe à la fin de
    /// <paramref name="text"/>. Un mot = séquence <c>[A-Za-z_][A-Za-z0-9_]*</c>
    /// qui n'est pas précédée d'un caractère alphanumérique (donc située après
    /// un séparateur = espace, opérateur, début de chaîne). Retourne
    /// <see cref="string.Empty"/> si rien à suggérer.
    /// </summary>
    private static string ExtrairePrefixeCourant(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var fin = text.Length - 1;
        // recule tant qu'on est sur un caractère de mot
        var debut = fin;
        while (debut >= 0 && (char.IsLetterOrDigit(text[debut]) || text[debut] == '_'))
            debut--;
        // debut pointe sur le séparateur précédent (ou -1) ; le mot est [debut+1 .. fin]
        if (fin == debut) return string.Empty;
        return text.Substring(debut + 1, fin - debut);
    }

    private void ValiderFormuleInterne(string expression)
    {
        var parse = FormulaParser.Parser(expression);
        if (parse.IsFailure)
        {
            FormuleValidation = $"Formule invalide : {parse.Error.Message}";
            FormuleValidationEstValide = false;
            FormuleValidationNbNoeuds = null;
            return;
        }
        var nbNoeuds = FormulaNodeWalker.Compter(parse.Value);
        var pluriel = nbNoeuds > 1 ? "s" : string.Empty;
        FormuleValidation = $"✓ Formule valide — {nbNoeuds} nœud{pluriel}";
        FormuleValidationEstValide = true;
        FormuleValidationNbNoeuds = nbNoeuds;
    }

    // ====================================================================
    // P10 — auto-complétion
    // ====================================================================

    [RelayCommand]
    private async Task DemanderCompletionAsync()
    {
        if (_completion is null)
        {
            CompletionPopupOuvert = false;
            return;
        }
        var prefixe = CompletionPrefixe?.Trim() ?? string.Empty;
        if (prefixe.Length == 0)
        {
            CompletionItems.Clear();
            CompletionPopupOuvert = false;
            return;
        }
        var items = await _completion.ProposerAsync(prefixe);
        CompletionItems.Clear();
        foreach (var it in items) CompletionItems.Add(it);
        CompletionPopupOuvert = CompletionItems.Count > 0;
    }

    [RelayCommand]
    private void InsererCompletion(CompletionItem? item)
    {
        if (item is null) return;
        // P10 — insertion "intelligente" : on remplace le préfixe en cours
        // de frappe (le dernier mot) par le Token choisi. Si l'expression est
        // vide, on pose simplement le token. Le popup se ferme, et le partial
        // OnFormuleExpressionChanged rejouera la validation live + re-extraction
        // du nouveau préfixe (vide, donc popup reste fermé).
        if (string.IsNullOrEmpty(FormuleExpression))
        {
            FormuleExpression = item.Token;
        }
        else
        {
            var longueurPrefixe = CompletionPrefixe?.Length ?? 0;
            var idxDebut = FormuleExpression.Length - longueurPrefixe;
            if (longueurPrefixe > 0 && idxDebut >= 0)
            {
                FormuleExpression = FormuleExpression.Substring(0, idxDebut) + item.Token;
            }
            else
            {
                // fallback : pas de préfixe identifiable, on colle en fin
                FormuleExpression = FormuleExpression.TrimEnd() + " " + item.Token;
            }
        }
        CompletionPopupOuvert = false;
    }

    [RelayCommand]
    private void FermerCompletion()
    {
        CompletionPopupOuvert = false;
    }

    // ====================================================================
    // P10 — simulation agent témoin
    // ====================================================================

    [RelayCommand]
    private async Task ChargerAgentsTemoinsAsync()
    {
        if (_agents is null) return;
        var res = await _agents.ListerAsync();
        if (res.IsFailure) return;
        AgentsTemoins.Clear();
        foreach (var a in res.Value) AgentsTemoins.Add(a);
    }

    [RelayCommand]
    private async Task SimulerAsync()
    {
        if (_simulateur is null)
        {
            ResultatSimulation = "Simulation non disponible (services P10 non câblés).";
            return;
        }
        if (AgentTemoinSelectionne is null)
        {
            ResultatSimulation = "Sélectionnez un agent témoin.";
            return;
        }
        if (string.IsNullOrWhiteSpace(FormuleRubriqueId))
        {
            ResultatSimulation = "Saisissez le code rubrique (champ ci-dessus) pour identifier la rubrique simulée.";
            return;
        }
        SimulationEnCours = true;
        ResultatSimulation = "Simulation en cours…";
        try
        {
            var res = await _simulateur.ExecuterAsync(new SimulerBulletinPourFormule.Demande(
                AgentId: AgentTemoinSelectionne.Id,
                DatePaie: DatePaieSimulation,
                RubriqueIdOverride: FormuleRubriqueId,
                ExpressionOverride: FormuleExpression));
            if (res.IsFailure)
            {
                ResultatSimulation = $"✗ Échec : {res.Error.Message}";
                NetBaseline = NetOverride = DeltaNet = null;
                return;
            }
            var sim = res.Value;
            NetBaseline = sim.BulletinBaseline.Net.Amount;
            NetOverride = sim.Bulletin.Net.Amount;
            DeltaNet = sim.DeltaNet;
            var signe = sim.DeltaNet >= 0 ? "+" : string.Empty;
            ResultatSimulation =
                $"Net baseline {sim.BulletinBaseline.Net.Amount:N0} DA | " +
                $"Net override {sim.Bulletin.Net.Amount:N0} DA | " +
                $"Δ {signe}{sim.DeltaNet:N0} DA";
        }
        finally
        {
            SimulationEnCours = false;
        }
    }

    // ====================================================================
    // Use cases existants (C4.1)
    // ====================================================================

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
            _navigation.OpenTab<FicheRubriqueViewModel>("Fiche rubrique");
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
