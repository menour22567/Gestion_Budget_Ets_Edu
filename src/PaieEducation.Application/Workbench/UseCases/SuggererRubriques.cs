using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Use case Workbench (Phase 5, tâche 5, D5, J3H lot 3) : suggère les
/// rubriques affectables auxquelles un agent est éligible à une date donnée,
/// via le moteur DNF (<see cref="RegleEligibiliteEvaluator"/>).
/// </summary>
/// <remarks>
/// Limité aux rubriques dont l'éligibilité repose sur au moins un groupe DNF
/// (<c>GroupeId</c> non nul) — une rubrique affectable qui n'a que des
/// conditions communes (sans groupe) n'a pas de <c>GroupeId</c> à citer dans
/// <c>Origine</c> ; elle est ignorée dans cette tranche. Idempotent : une
/// suggestion déjà présente n'est jamais dupliquée
/// (<see cref="IAgentRubriqueRepository.SuggererAsync"/>). N'écrit jamais
/// d'<c>AvertissementsHistorique</c> — ça relève de l'acceptation, pas de la
/// suggestion (J3H §10(a)).
/// </remarks>
public sealed class SuggererRubriques
{
    /// <summary>Demande de suggestion pour un agent à une date de paie.</summary>
    public sealed record Demande(string AgentId, string DatePaie);

    private readonly IAgentCarriereRepository _agents;
    private readonly IWorkbenchReadRepository _workbench;
    private readonly IAgentRubriqueRepository _agentRubriques;
    private readonly IClock _clock;
    private readonly RegleEligibiliteEvaluator _evaluator = new(new CritereEligibiliteResolver());

    public SuggererRubriques(
        IAgentCarriereRepository agents, IWorkbenchReadRepository workbench,
        IAgentRubriqueRepository agentRubriques, IClock clock)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
        _agentRubriques = agentRubriques ?? throw new ArgumentNullException(nameof(agentRubriques));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<IReadOnlyList<string>>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);

        var agent = await _agents.ResoudreAsync(demande.AgentId, demande.DatePaie, ct);
        if (agent.IsFailure)
            return Result.Failure<IReadOnlyList<string>>(agent.Error);

        var criteres = await _workbench.ListerCriteresParIdAsync(ct);
        var rubriques = await _workbench.ListerRubriquesAffectablesAsync(demande.DatePaie, ct);

        var suggerees = new List<string>();
        var creeLe = _clock.UtcNow;

        foreach (var rubriqueId in rubriques)
        {
            var conditions = await _workbench.ListerConditionsParRubriqueAsync(rubriqueId, demande.DatePaie, ct);
            if (!conditions.Any(c => c.GroupeId is not null))
                continue; // Hors périmètre de cette tranche — pas de groupe DNF à citer.

            var resultat = _evaluator.Evaluer(rubriqueId, agent.Value, demande.DatePaie, conditions, criteres);
            if (!resultat.EstEligible)
                continue;

            var groupeSatisfaitId = resultat.Groupes
                .FirstOrDefault(g => g.GroupeId is not null && g.Satisfait)?.GroupeId;
            if (groupeSatisfaitId is null)
                continue; // Défensif : ne devrait pas arriver si EstEligible et des groupes existent.

            var groupes = await _workbench.ListerGroupesParRubriqueAsync(rubriqueId, demande.DatePaie, ct);
            var groupe = groupes.FirstOrDefault(g => g.Id == groupeSatisfaitId);
            if (groupe is null)
                continue; // Défensif : groupe non trouvé (incohérence de données).

            var origine = $"GROUPE:{groupe.Id}@{groupe.Periode.DateEffet}";
            var cree = await _agentRubriques.SuggererAsync(
                demande.AgentId, rubriqueId, occurrence: 1, origine, demande.DatePaie, creeLe, ct);
            if (cree.IsFailure)
                return Result.Failure<IReadOnlyList<string>>(cree.Error);
            if (cree.Value is not null)
                suggerees.Add(rubriqueId);
        }

        return Result.Success<IReadOnlyList<string>>(suggerees);
    }
}
