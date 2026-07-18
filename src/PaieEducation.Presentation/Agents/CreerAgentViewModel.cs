using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Application.Agents.UseCases;
using PaieEducation.Application.Referentiels.UseCases;
using PaieEducation.Domain.Agents;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Domain.Calcul.ValueObjects;
using PaieEducation.Presentation.Dialogs;

namespace PaieEducation.Presentation.Agents;

/// <summary>
/// Écran « Créer un agent » (Phase 6, tâche 3 — gestion agents/carrière),
/// appelle le use case <see cref="CreerAgent"/> (déjà livré, Phase 5).
/// </summary>
/// <remarks>
/// Les listes de <c>Sexe</c>/<c>SituationFamiliale</c>/<c>TypeContrat</c>
/// sont chargées depuis les tables <c>TypesSexe</c>, <c>SituationsFamiliales</c>,
/// <c>TypesContrat</c> au montage de l'écran (<see cref="ChargerReferentielsCommand"/>).
/// </remarks>
public sealed partial class CreerAgentViewModel : ObservableObject
{
    public ObservableCollection<string> SexesDisponibles { get; } = [];
    public ObservableCollection<string> SituationsDisponibles { get; } = [];
    public ObservableCollection<string> TypesContratDisponibles { get; } = [];

    public ObservableCollection<ReferentielItem> GradesDisponibles { get; } = [];
    public ObservableCollection<ReferentielItem> CategoriesDisponibles { get; } = [];
    public ObservableCollection<ReferentielItem> EchelonsDisponibles { get; } = [];

    private readonly CreerAgent _creerAgent;
    private readonly ListerReferentiels _listerReferentiels;
    private readonly IAgentReadRepository _agentRead;
    private readonly IDialogService _dialogs;

    [ObservableProperty] private string matricule = string.Empty;
    [ObservableProperty] private string nom = string.Empty;
    [ObservableProperty] private string prenom = string.Empty;
    [ObservableProperty] private string dateNaissance = string.Empty;
    [ObservableProperty] private string dateRecrutement = string.Empty;
    [ObservableProperty] private string sexe = "M";
    [ObservableProperty] private string situationFamiliale = "CELIBATAIRE";
    [ObservableProperty] private ReferentielItem? gradeSelectionne;
    [ObservableProperty] private ReferentielItem? categorieSelectionnee;
    [ObservableProperty] private ReferentielItem? echelonSelectionne;
    [ObservableProperty] private string typeContrat = "STATUTAIRE";
    [ObservableProperty] private string? resultat;
    [ObservableProperty] private bool enCours;

    public CreerAgentViewModel(CreerAgent creerAgent, ListerReferentiels listerReferentiels,
        IAgentReadRepository agentRead, IDialogService dialogs)
    {
        _creerAgent = creerAgent ?? throw new ArgumentNullException(nameof(creerAgent));
        _listerReferentiels = listerReferentiels ?? throw new ArgumentNullException(nameof(listerReferentiels));
        _agentRead = agentRead ?? throw new ArgumentNullException(nameof(agentRead));
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

        var sexes = await _agentRead.ListerSexesAsync();
        if (sexes.IsFailure) { await _dialogs.ShowErrorAsync(sexes.Error.Message); return; }
        SexesDisponibles.Clear();
        foreach (var s in sexes.Value) SexesDisponibles.Add(s.Id);
        if (!sexes.Value.Any(s => s.Id == Sexe))
            Sexe = sexes.Value[0].Id;

        var situations = await _agentRead.ListerSituationsFamilialesAsync();
        if (situations.IsFailure) { await _dialogs.ShowErrorAsync(situations.Error.Message); return; }
        SituationsDisponibles.Clear();
        foreach (var s in situations.Value) SituationsDisponibles.Add(s.Id);
        if (!situations.Value.Any(s => s.Id == SituationFamiliale))
            SituationFamiliale = situations.Value[0].Id;

        var contrats = await _agentRead.ListerTypesContratAsync();
        if (contrats.IsFailure) { await _dialogs.ShowErrorAsync(contrats.Error.Message); return; }
        TypesContratDisponibles.Clear();
        foreach (var c in contrats.Value) TypesContratDisponibles.Add(c.Id);
        if (!contrats.Value.Any(c => c.Id == TypeContrat))
            TypeContrat = contrats.Value[0].Id;
    }

    [RelayCommand]
    private async Task CreerAsync()
    {
        EnCours = true;
        Resultat = null;
        try
        {
            var demande = new NouvelAgent(
                Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, SituationFamiliale,
                GradeSelectionne?.Id ?? string.Empty, CategorieSelectionnee?.Id ?? string.Empty,
                EchelonSelectionne?.Id ?? string.Empty, TypeContrat);
            var result = await _creerAgent.ExecuterAsync(demande);
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }

            Resultat = $"Agent créé : {result.Value}";
        }
        finally
        {
            EnCours = false;
        }
    }
}
