using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Presentation.Dialogs;
using SuggererRubriquesUseCase = PaieEducation.Application.Workbench.UseCases.SuggererRubriques;

namespace PaieEducation.Presentation.Workbench;

/// <summary>
/// Écran « Suggérer des rubriques » (Phase 6, tâche 3) — évalue les
/// rubriques affectables auxquelles un agent est éligible via le use case
/// <see cref="SuggererRubriquesUseCase"/> (déjà livré, Phase 5, D5), crée
/// les suggestions (<c>AgentRubriques</c>, Statut=SUGGEREE), puis liste
/// toutes les affectations de l'agent (<see cref="ListerAffectationsAgent"/>)
/// avec les actions de la machine à états J3H §7
/// (<see cref="AccepterSuggestion"/>/<see cref="SuspendreAffectation"/>/
/// <see cref="SupprimerAffectation"/>, Phase 5).
/// </summary>
/// <remarks>
/// Alias <c>SuggererRubriquesUseCase</c> pour éviter la collision de nom
/// avec ce ViewModel lui-même. Après chaque action, <see cref="Affectations"/>
/// est rechargée depuis la base (jamais de mise à jour optimiste côté
/// client) — le <c>Statut</c> affiché reflète toujours l'état réel. Sélecteur
/// d'agent (<see cref="Agents"/>, même patron que <c>CalculerBulletinViewModel</c>)
/// plutôt qu'une saisie libre de GUID.
/// </remarks>
public sealed partial class SuggererRubriquesViewModel : ObservableObject
{
    private readonly SuggererRubriquesUseCase _suggererRubriques;
    private readonly ListerAffectationsAgent _listerAffectations;
    private readonly AccepterSuggestion _accepterSuggestion;
    private readonly SupprimerAffectation _supprimerAffectation;
    private readonly SuspendreAffectation _suspendreAffectation;
    private readonly IAgentReadRepository _agentsRead;
    private readonly IDialogService _dialogs;

    public ObservableCollection<AgentResume> Agents { get; } = [];

    [ObservableProperty]
    private AgentResume? agentSelectionne;

    [ObservableProperty]
    private string agentId = string.Empty;

    [ObservableProperty]
    private string datePaie = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");

    [ObservableProperty]
    private string? resultat;

    [ObservableProperty]
    private bool enCours;

    public ObservableCollection<AffectationRubrique> Affectations { get; } = [];

    public SuggererRubriquesViewModel(
        SuggererRubriquesUseCase suggererRubriques, ListerAffectationsAgent listerAffectations,
        AccepterSuggestion accepterSuggestion, SupprimerAffectation supprimerAffectation,
        SuspendreAffectation suspendreAffectation, IAgentReadRepository agentsRead, IDialogService dialogs)
    {
        _suggererRubriques = suggererRubriques ?? throw new ArgumentNullException(nameof(suggererRubriques));
        _listerAffectations = listerAffectations ?? throw new ArgumentNullException(nameof(listerAffectations));
        _accepterSuggestion = accepterSuggestion ?? throw new ArgumentNullException(nameof(accepterSuggestion));
        _supprimerAffectation = supprimerAffectation ?? throw new ArgumentNullException(nameof(supprimerAffectation));
        _suspendreAffectation = suspendreAffectation ?? throw new ArgumentNullException(nameof(suspendreAffectation));
        _agentsRead = agentsRead ?? throw new ArgumentNullException(nameof(agentsRead));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        _ = ChargerAgentsCommand.ExecuteAsync(null);
    }

    /// <summary>Reflète la sélection du ComboBox dans l'identifiant utilisé par le use case.</summary>
    partial void OnAgentSelectionneChanged(AgentResume? value) => AgentId = value?.Id ?? string.Empty;

    [RelayCommand]
    private async Task ChargerAgentsAsync()
    {
        var result = await _agentsRead.ListerAsync();
        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync(result.Error.Message);
            return;
        }

        Agents.Clear();
        foreach (var a in result.Value)
            Agents.Add(a);
    }

    [RelayCommand]
    private async Task SuggererAsync()
    {
        EnCours = true;
        Resultat = null;
        try
        {
            var demande = new SuggererRubriquesUseCase.Demande(AgentId, DatePaie);
            var result = await _suggererRubriques.ExecuterAsync(demande);
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }

            Resultat = result.Value.Count == 0
                ? "Aucune rubrique suggérée."
                : $"Rubriques suggérées : {string.Join(", ", result.Value)}";
            await ActualiserAsync();
        }
        finally
        {
            EnCours = false;
        }
    }

    [RelayCommand]
    private async Task ActualiserAsync()
    {
        var result = await _listerAffectations.ExecuterAsync(new ListerAffectationsAgent.Demande(AgentId, DatePaie));
        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync(result.Error.Message);
            return;
        }

        Affectations.Clear();
        foreach (var affectation in result.Value)
            Affectations.Add(affectation);
    }

    [RelayCommand]
    private async Task AccepterAsync(string agentRubriqueId)
        => await TransitionnerAsync(() => _accepterSuggestion.ExecuterAsync(new AccepterSuggestion.Demande(agentRubriqueId)));

    [RelayCommand]
    private async Task SuspendreAsync(string agentRubriqueId)
        => await TransitionnerAsync(() => _suspendreAffectation.ExecuterAsync(new SuspendreAffectation.Demande(agentRubriqueId)));

    [RelayCommand]
    private async Task SupprimerAsync(string agentRubriqueId)
        => await TransitionnerAsync(() => _supprimerAffectation.ExecuterAsync(new SupprimerAffectation.Demande(agentRubriqueId)));

    private async Task TransitionnerAsync(Func<Task<Result<string>>> transition)
    {
        var result = await transition();
        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync(result.Error.Message);
            return;
        }

        await ActualiserAsync();
    }
}
