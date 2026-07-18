using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Workbench.Constants;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Use case Workbench (Phase 5, tâche 5, J3H §7) : accepte une affectation
/// suggérée (<c>SUGGEREE → ACCEPTEE</c>) ou réactive une affectation
/// suspendue (<c>SUSPENDUE → ACCEPTEE</c>, même cible sur le diagramme d'état) —
/// aucune précondition sur l'état de départ (« aucune transition n'est
/// bloquée par une règle »), seule la sortie de l'état terminal
/// <c>SUPPRIMEE</c> est refusée par <see cref="IAgentRubriqueRepository.ChangerStatutAsync"/>.
/// </summary>
public sealed class AccepterSuggestion
{
    public sealed record Demande(string AgentRubriqueId);

    private readonly IAgentRubriqueRepository _agentRubriques;
    private readonly IClock _clock;

    public AccepterSuggestion(IAgentRubriqueRepository agentRubriques, IClock clock)
    {
        _agentRubriques = agentRubriques ?? throw new ArgumentNullException(nameof(agentRubriques));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<string>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        Guard.AgainstNullOrWhiteSpace(demande.AgentRubriqueId);

        return await _agentRubriques.ChangerStatutAsync(demande.AgentRubriqueId, StatutAffectation.Acceptee, _clock.UtcNow, ct);
    }
}
