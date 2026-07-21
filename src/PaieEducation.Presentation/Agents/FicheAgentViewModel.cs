using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Application.Agents.UseCases;
using PaieEducation.Application.Referentiels.UseCases;
using PaieEducation.Domain.Agents;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Domain.Calcul.Constants;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.ValueObjects;
using PaieEducation.Domain.Workbench.Constants;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Presentation.Dialogs;

namespace PaieEducation.Presentation.Agents;

/// <summary>
/// Écran « Fiche agent » (chantier gestion des agents) — consultation en
/// lecture seule de l'identité et de la carrière la plus récente d'un agent
/// via <see cref="ConsulterFicheAgent"/>, complétée par 3 formulaires
/// d'écriture (chantier D) : modifier l'identité (<see cref="ModifierAgent"/>),
/// enregistrer un nouvel événement de carrière — avancement d'échelon,
/// promotion, mutation — (<see cref="EnregistrerEvenementCarriere"/>), définir
/// un attribut versionné (<see cref="DefinirAttributAgent"/>). Atteignable en
/// drill-down préchargé depuis <see cref="ListeAgentsViewModel"/> (le VM
/// source affecte <see cref="AgentId"/> puis exécute
/// <see cref="ChargerCommand"/>, même convention que
/// <c>MatriceCouvertureViewModel.NaviguerVersFiche</c>), ou en autonome via la
/// saisie de l'identifiant (même convention que <c>FicheRubriqueViewModel</c>).
/// </summary>
/// <remarks>
/// <see cref="ChargerAsync"/> charge d'abord les référentiels de sélecteurs
/// (<see cref="ChargerReferentielsCommand"/>) puis la fiche, afin que le
/// pré-remplissage des formulaires d'édition (identité, carrière) trouve
/// toujours les listes déjà peuplées — élimine toute course entre les deux
/// chargements asynchrones (patron plus robuste que le chargement fire-and-
/// forget au constructeur utilisé ailleurs, nécessaire ici car le
/// pré-remplissage dépend directement du contenu des listes).
/// </remarks>
public sealed partial class FicheAgentViewModel : ObservableObject
{
    private static readonly string[] StatutsValides = ["ACTIF", "SUSPENDU", "RADIE"];

    // Clés d'attribut connues du moteur de calcul (NOTATION_AGENT→PAPP,
    // ORIGINE_STATUTAIRE→éligibilité ISSRP, ANCIENNETE_PRIVEE_ANNEES→bonif.
    // d'ancienneté) — liste des clés reconnues, pas une donnée réglementaire
    // (celles-ci restent en saisie libre, cf. <see cref="AttributValeur"/>).
    private const string AttributAncienntePriveeAnnees = "ANCIENNETE_PRIVEE_ANNEES";

    private readonly ConsulterFicheAgent _consulterFicheAgent;
    private readonly ModifierAgent _modifierAgent;
    private readonly EnregistrerEvenementCarriere _enregistrerEvenementCarriere;
    private readonly DefinirAttributAgent _definirAttributAgent;
    private readonly ListerReferentiels _listerReferentiels;
    private readonly IAgentReadRepository _agentRead;
    private readonly IAgentCarriereRepository _agentCarriere;
    private readonly IDialogService _dialogs;

    [ObservableProperty] private string agentId = string.Empty;
    [ObservableProperty] private bool enCours;
    [ObservableProperty] private AgentDetail? detail;

    /// <summary>Vrai lorsqu'une fiche est chargée — pilote l'affichage du bloc détail et des formulaires d'édition.</summary>
    public bool HasDetail => Detail is not null;

    partial void OnDetailChanged(AgentDetail? value)
    {
        OnPropertyChanged(nameof(HasDetail));
        if (value is not null) PreRemplirFormulaires(value);
    }

    // -- Sources des sélecteurs --
    public ObservableCollection<string> SexesDisponibles { get; } = [];
    public ObservableCollection<string> SituationsDisponibles { get; } = [];
    public ObservableCollection<string> TypesContratDisponibles { get; } = [];
    public ObservableCollection<string> StatutsDisponibles { get; } = [.. StatutsValides];
    public ObservableCollection<ReferentielItem> GradesDisponibles { get; } = [];
    public ObservableCollection<ReferentielItem> CategoriesDisponibles { get; } = [];
    public ObservableCollection<ReferentielItem> EchelonsDisponibles { get; } = [];
    public ObservableCollection<ReferentielItem> FonctionsDisponibles { get; } = [];
    public ObservableCollection<ReferentielItem> EtablissementsDisponibles { get; } = [];
    public ObservableCollection<string> AttributsConnus { get; } =
        [SourceValeurCodes.NotationAgent, CritereIds.OrigineStatutaire, AttributAncienntePriveeAnnees];

    // -- Modifier l'identité --
    [ObservableProperty] private string modifierNom = string.Empty;
    [ObservableProperty] private string modifierPrenom = string.Empty;
    [ObservableProperty] private string modifierDateNaissance = string.Empty;
    [ObservableProperty] private string modifierSexe = string.Empty;
    [ObservableProperty] private string modifierSituationFamiliale = string.Empty;
    [ObservableProperty] private string modifierStatut = string.Empty;
    [ObservableProperty] private bool identiteEnCours;
    [ObservableProperty] private string? identiteResultat;

    // -- Nouvel événement de carrière --
    [ObservableProperty] private ReferentielItem? nouvelleCarriereGrade;
    [ObservableProperty] private ReferentielItem? nouvelleCarriereCategorie;
    [ObservableProperty] private ReferentielItem? nouvelleCarriereEchelon;
    [ObservableProperty] private ReferentielItem? nouvelleCarriereFonction;
    [ObservableProperty] private ReferentielItem? nouvelleCarriereEtablissement;
    [ObservableProperty] private string nouvelleCarriereTypeContrat = string.Empty;
    [ObservableProperty] private string nouvelleCarriereDateEffet = string.Empty;
    [ObservableProperty] private string nouvelleCarriereMotif = string.Empty;
    [ObservableProperty] private string? nouvelleCarriereNumeroDecision;
    [ObservableProperty] private bool carriereEnCours;
    [ObservableProperty] private string? carriereResultat;

    // -- Définir un attribut --
    [ObservableProperty] private string attributCle = SourceValeurCodes.NotationAgent;
    [ObservableProperty] private string attributValeur = string.Empty;
    [ObservableProperty] private string attributDateEffet = string.Empty;
    [ObservableProperty] private string? attributSource;
    [ObservableProperty] private bool attributEnCours;
    [ObservableProperty] private string? attributResultat;

    // -- Valeurs actuelles résolues (lecture seule, évite d'écrire à l'aveugle) --
    [ObservableProperty] private decimal? attributNoteActuelle;
    [ObservableProperty] private string? attributOrigineActuelle;
    [ObservableProperty] private int? attributAnciennetePriveeActuelle;

    public FicheAgentViewModel(
        ConsulterFicheAgent consulterFicheAgent, ModifierAgent modifierAgent,
        EnregistrerEvenementCarriere enregistrerEvenementCarriere, DefinirAttributAgent definirAttributAgent,
        ListerReferentiels listerReferentiels, IAgentReadRepository agentRead,
        IAgentCarriereRepository agentCarriere, IDialogService dialogs)
    {
        _consulterFicheAgent = consulterFicheAgent ?? throw new ArgumentNullException(nameof(consulterFicheAgent));
        _modifierAgent = modifierAgent ?? throw new ArgumentNullException(nameof(modifierAgent));
        _enregistrerEvenementCarriere = enregistrerEvenementCarriere ?? throw new ArgumentNullException(nameof(enregistrerEvenementCarriere));
        _definirAttributAgent = definirAttributAgent ?? throw new ArgumentNullException(nameof(definirAttributAgent));
        _listerReferentiels = listerReferentiels ?? throw new ArgumentNullException(nameof(listerReferentiels));
        _agentRead = agentRead ?? throw new ArgumentNullException(nameof(agentRead));
        _agentCarriere = agentCarriere ?? throw new ArgumentNullException(nameof(agentCarriere));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        _ = ChargerReferentielsCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task ChargerReferentielsAsync()
    {
        var listerResult = await _listerReferentiels.ExecuterAsync();
        if (listerResult.IsFailure)
        {
            await _dialogs.ShowErrorAsync(listerResult.Error.Message);
            return;
        }

        GradesDisponibles.Clear();
        foreach (var g in listerResult.Value.Grades) GradesDisponibles.Add(g);
        CategoriesDisponibles.Clear();
        foreach (var c in listerResult.Value.Categories) CategoriesDisponibles.Add(c);
        EchelonsDisponibles.Clear();
        foreach (var e in listerResult.Value.Echelons) EchelonsDisponibles.Add(e);
        FonctionsDisponibles.Clear();
        foreach (var f in listerResult.Value.Fonctions) FonctionsDisponibles.Add(f);
        EtablissementsDisponibles.Clear();
        foreach (var e in listerResult.Value.Etablissements) EtablissementsDisponibles.Add(e);

        var sexes = await _agentRead.ListerSexesAsync();
        if (sexes.IsFailure) { await _dialogs.ShowErrorAsync(sexes.Error.Message); return; }
        SexesDisponibles.Clear();
        foreach (var s in sexes.Value) SexesDisponibles.Add(s.Id);

        var situations = await _agentRead.ListerSituationsFamilialesAsync();
        if (situations.IsFailure) { await _dialogs.ShowErrorAsync(situations.Error.Message); return; }
        SituationsDisponibles.Clear();
        foreach (var s in situations.Value) SituationsDisponibles.Add(s.Id);

        var contrats = await _agentRead.ListerTypesContratAsync();
        if (contrats.IsFailure) { await _dialogs.ShowErrorAsync(contrats.Error.Message); return; }
        TypesContratDisponibles.Clear();
        foreach (var c in contrats.Value) TypesContratDisponibles.Add(c.Id);
    }

    [RelayCommand]
    private async Task ChargerAsync()
    {
        if (string.IsNullOrWhiteSpace(AgentId)) return;

        EnCours = true;
        try
        {
            // Référentiels d'abord : garantit que le pré-remplissage (déclenché
            // par OnDetailChanged juste après) trouve des listes déjà peuplées.
            await ChargerReferentielsCommand.ExecuteAsync(null);

            var result = await _consulterFicheAgent.ExecuterAsync(new ConsulterFicheAgent.Demande(AgentId));
            if (result.IsFailure)
            {
                Detail = null;
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }

            Detail = result.Value;
            await ChargerAttributsActuelsAsync();
        }
        finally
        {
            EnCours = false;
        }
    }

    /// <summary>
    /// Résout les valeurs d'attribut actuellement en vigueur (aujourd'hui) via
    /// le même port que le moteur de calcul — évite de définir un nouvel
    /// attribut sans savoir ce qu'il remplace. Purement informatif : un échec
    /// de résolution (ex. aucune carrière en vigueur aujourd'hui) laisse ces
    /// champs vides sans bloquer ni polluer d'erreur la fiche elle-même.
    /// </summary>
    private async Task ChargerAttributsActuelsAsync()
    {
        AttributNoteActuelle = null;
        AttributOrigineActuelle = null;
        AttributAnciennetePriveeActuelle = null;

        var aujourdhui = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        var result = await _agentCarriere.ResoudreAsync(AgentId, aujourdhui);
        if (result.IsFailure) return;

        AttributNoteActuelle = result.Value.Note;
        AttributOrigineActuelle = result.Value.OrigineStatutaire;
        AttributAnciennetePriveeActuelle = result.Value.AnciennetePriveeAnnees;
    }

    private void PreRemplirFormulaires(AgentDetail detail)
    {
        ModifierNom = detail.Nom;
        ModifierPrenom = detail.Prenom;
        ModifierDateNaissance = detail.DateNaissance;
        ModifierSexe = detail.Sexe;
        ModifierSituationFamiliale = detail.SituationFamiliale;
        ModifierStatut = detail.Statut;

        NouvelleCarriereGrade = GradesDisponibles.FirstOrDefault(g => g.Id == detail.GradeId);
        NouvelleCarriereCategorie = CategoriesDisponibles.FirstOrDefault(c => c.Id == detail.CategorieNiveau?.ToString());
        NouvelleCarriereEchelon = EchelonsDisponibles.FirstOrDefault(e => e.Id == detail.EchelonNumero?.ToString());
        NouvelleCarriereFonction = FonctionsDisponibles.FirstOrDefault(f => f.Id == detail.FonctionId);
        NouvelleCarriereEtablissement = EtablissementsDisponibles.FirstOrDefault(e => e.Id == detail.EtablissementId);
        NouvelleCarriereTypeContrat = detail.TypeContrat ?? string.Empty;
    }

    [RelayCommand]
    private async Task ModifierIdentiteAsync()
    {
        if (Detail is null) return;

        IdentiteEnCours = true;
        IdentiteResultat = null;
        try
        {
            var demande = new AgentModifie(
                Detail.Id, ModifierNom, ModifierPrenom, ModifierDateNaissance,
                ModifierSexe, ModifierSituationFamiliale, ModifierStatut);
            var result = await _modifierAgent.ExecuterAsync(demande);
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }

            IdentiteResultat = "Identité modifiée.";
            await ChargerAsync();
        }
        finally
        {
            IdentiteEnCours = false;
        }
    }

    [RelayCommand]
    private async Task EnregistrerCarriereAsync()
    {
        if (Detail is null) return;
        if (NouvelleCarriereGrade is null || NouvelleCarriereCategorie is null || NouvelleCarriereEchelon is null)
        {
            await _dialogs.ShowErrorAsync("Grade, catégorie et échelon sont requis.");
            return;
        }

        CarriereEnCours = true;
        CarriereResultat = null;
        try
        {
            var demande = new EvenementCarriere(
                Detail.Id, NouvelleCarriereGrade.Id, NouvelleCarriereCategorie.Id, NouvelleCarriereEchelon.Id,
                NouvelleCarriereTypeContrat, NouvelleCarriereDateEffet, NouvelleCarriereMotif,
                NouvelleCarriereFonction?.Id, NouvelleCarriereEtablissement?.Id,
                string.IsNullOrWhiteSpace(NouvelleCarriereNumeroDecision) ? null : NouvelleCarriereNumeroDecision);
            var result = await _enregistrerEvenementCarriere.ExecuterAsync(demande);
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }

            CarriereResultat = $"Événement de carrière enregistré (Id : {result.Value})";
            await ChargerAsync();
        }
        finally
        {
            CarriereEnCours = false;
        }
    }

    [RelayCommand]
    private async Task DefinirAttributAsync()
    {
        if (Detail is null) return;

        AttributEnCours = true;
        AttributResultat = null;
        try
        {
            var demande = new DefinirAttributAgent.Demande(
                Detail.Id, AttributCle, AttributValeur, AttributDateEffet,
                string.IsNullOrWhiteSpace(AttributSource) ? null : AttributSource);
            var result = await _definirAttributAgent.ExecuterAsync(demande);
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }

            AttributResultat = $"Attribut enregistré (Id : {result.Value})";
        }
        finally
        {
            AttributEnCours = false;
        }
    }
}
