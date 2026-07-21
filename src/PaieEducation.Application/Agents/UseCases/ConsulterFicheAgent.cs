using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Application.Agents.UseCases;

/// <summary>
/// Fiche de consultation d'un agent (chantier « gestion des agents ») —
/// renvoie l'identité complète et la carrière la plus récente. Lecture seule :
/// aucune modification d'agent ni de carrière n'existe encore (chantier
/// ultérieur). Même patron que <c>ConsulterFicheRubrique</c> : le
/// « non trouvé » du port de lecture est traduit ici en erreur métier.
/// </summary>
public sealed class ConsulterFicheAgent
{
    public sealed record Demande(string AgentId);

    private readonly IAgentReadRepository _agents;

    public ConsulterFicheAgent(IAgentReadRepository agents)
        => _agents = agents ?? throw new ArgumentNullException(nameof(agents));

    public async Task<Result<AgentDetail>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        Guard.AgainstNullOrWhiteSpace(demande.AgentId);

        var detail = await _agents.ObtenirAsync(demande.AgentId, ct);
        if (detail is null)
            return Result.Failure<AgentDetail>(Error.NotFound($"Agent '{demande.AgentId}' introuvable."));

        return Result.Success(detail);
    }
}
