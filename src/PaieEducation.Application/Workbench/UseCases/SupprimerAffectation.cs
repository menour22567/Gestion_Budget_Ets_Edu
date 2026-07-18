using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Workbench.Constants;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Use case Workbench (Phase 5, tâche 5, J3H §7) : refuse une suggestion ou
/// retire une affectation (<c>→ SUPPRIMEE</c>, état terminal — « réaffecter
/// = NOUVELLE ligne, l'ancienne reste en trace »). Aucune précondition sur
/// l'état de départ ; échoue explicitement si la ligne est déjà
/// <c>SUPPRIMEE</c> (<see cref="IAgentRubriqueRepository.ChangerStatutAsync"/>).
/// </summary>
public sealed class SupprimerAffectation
{
    public sealed record Demande(string AgentRubriqueId);

    private readonly IAgentRubriqueRepository _agentRubriques;
    private readonly IClock _clock;

    public SupprimerAffectation(IAgentRubriqueRepository agentRubriques, IClock clock)
    {
        _agentRubriques = agentRubriques ?? throw new ArgumentNullException(nameof(agentRubriques));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<string>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        Guard.AgainstNullOrWhiteSpace(demande.AgentRubriqueId);

        return await _agentRubriques.ChangerStatutAsync(demande.AgentRubriqueId, StatutAffectation.Supprimee, _clock.UtcNow, ct);
    }
}
