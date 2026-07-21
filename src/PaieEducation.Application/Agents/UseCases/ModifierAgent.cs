using PaieEducation.Domain.Agents;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Agents.UseCases;

/// <summary>
/// Modifie l'identité d'un agent existant (chantier « gestion des agents »).
/// Même validation des valeurs énumérées que <see cref="CreerAgent"/>
/// (<c>Sexe</c>/<c>SituationFamiliale</c> contre les tables de nomenclature) ;
/// <c>Statut</c> est une contrainte <c>CHECK</c> directe sans table dédiée
/// (3 valeurs fixes, comme <c>Natures</c>/<c>Periodicites</c> dans
/// <c>EditerRubriqueViewModel</c>).
/// </summary>
public sealed class ModifierAgent
{
    private static readonly string[] StatutsValides = ["ACTIF", "SUSPENDU", "RADIE"];

    private readonly IAgentRepository _agents;
    private readonly IAgentReadRepository _agentRead;
    private readonly IClock _clock;

    public ModifierAgent(IAgentRepository agents, IAgentReadRepository agentRead, IClock clock)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _agentRead = agentRead ?? throw new ArgumentNullException(nameof(agentRead));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<string>> ExecuterAsync(AgentModifie demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        Guard.AgainstNullOrWhiteSpace(demande.AgentId);
        Guard.AgainstNullOrWhiteSpace(demande.Nom);
        Guard.AgainstNullOrWhiteSpace(demande.Prenom);
        Guard.AgainstNullOrWhiteSpace(demande.DateNaissance);

        var sexes = await _agentRead.ListerSexesAsync(ct);
        if (sexes.IsFailure) return Result.Failure<string>(sexes.Error);
        if (!sexes.Value.Any(s => s.Id == demande.Sexe))
            return Result.Failure<string>(Error.Validation($"Sexe invalide : '{demande.Sexe}'."));

        var situations = await _agentRead.ListerSituationsFamilialesAsync(ct);
        if (situations.IsFailure) return Result.Failure<string>(situations.Error);
        if (!situations.Value.Any(s => s.Id == demande.SituationFamiliale))
            return Result.Failure<string>(Error.Validation(
                $"Situation familiale invalide : '{demande.SituationFamiliale}'."));

        if (!StatutsValides.Contains(demande.Statut))
            return Result.Failure<string>(Error.Validation($"Statut invalide : '{demande.Statut}'."));

        return await _agents.ModifierAsync(demande, _clock.UtcNow, ct);
    }
}
