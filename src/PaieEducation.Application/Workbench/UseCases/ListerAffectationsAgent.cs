using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Use case Workbench (Phase 6, tâche 3) : liste les affectations
/// (<c>AgentRubriques</c>) d'un agent couvrant une date de paie — y compris
/// <c>SUPPRIMEE</c> (traçabilité complète, J3H §7). Lecture seule ; enveloppe
/// <see cref="IAgentRubriqueRepository.ListerParAgentAsync"/>, même patron
/// que <see cref="ConsulterBulletin"/> enveloppant <c>IBulletinReadRepository</c>.
/// </summary>
public sealed class ListerAffectationsAgent
{
    public sealed record Demande(string AgentId, string DatePaie);

    private readonly IAgentRubriqueRepository _agentRubriques;

    public ListerAffectationsAgent(IAgentRubriqueRepository agentRubriques)
        => _agentRubriques = agentRubriques ?? throw new ArgumentNullException(nameof(agentRubriques));

    public async Task<Result<IReadOnlyList<AffectationRubrique>>> ExecuterAsync(
        Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        Guard.AgainstNullOrWhiteSpace(demande.AgentId);
        Guard.AgainstNullOrWhiteSpace(demande.DatePaie);

        return await _agentRubriques.ListerParAgentAsync(demande.AgentId, demande.DatePaie, ct);
    }
}
