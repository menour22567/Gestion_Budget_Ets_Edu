using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Domain.Agents.Repositories;

/// <summary>
/// Crée un agent et sa carrière initiale. Port du Domain implémenté par
/// <c>Infrastructure.Repositories.Agents.AgentRepository</c> (I/O réelle —
/// inaccessible depuis <c>Application</c>, cf.
/// <c>DependencyRulesTests.Application_ne_depend_ni_de_infrastructure_ni_de_persistence</c>).
/// </summary>
public interface IAgentRepository
{
    /// <summary>
    /// Crée l'agent et sa carrière initiale en une seule transaction. Échoue
    /// explicitement (<see cref="Error.Conflict"/>) si le matricule est déjà
    /// utilisé — jamais d'exception pour ce cas métier plausible.
    /// </summary>
    /// <returns>L'Id (GUID) du nouvel agent, en cas de succès.</returns>
    Task<Result<string>> CreerAsync(NouvelAgent demande, DateTimeOffset creeLe, CancellationToken ct = default);

    /// <summary>
    /// Modifie l'identité d'un agent existant (simple UPDATE — <c>Agents</c>
    /// n'est pas une table versionnée). Échoue explicitement
    /// (<see cref="Error.NotFound"/>) si l'agent n'existe pas.
    /// </summary>
    /// <returns>L'Id de l'agent modifié, en cas de succès.</returns>
    Task<Result<string>> ModifierAsync(AgentModifie demande, DateTimeOffset modifieLe, CancellationToken ct = default);

    /// <summary>
    /// Enregistre un nouvel événement de carrière : ferme la carrière en
    /// vigueur (<c>DateFin</c>) et insère la nouvelle version. Échoue
    /// explicitement (<see cref="Error.NotFound"/> si l'agent n'existe pas,
    /// <see cref="Error.Conflict"/> si une carrière existe déjà à cette date
    /// d'effet, <see cref="Error.Validation"/> si la date d'effet n'est pas
    /// postérieure à la carrière en vigueur).
    /// </summary>
    /// <returns>L'Id (GUID) de la nouvelle ligne de carrière, en cas de succès.</returns>
    Task<Result<string>> EnregistrerEvenementCarriereAsync(
        EvenementCarriere demande, DateTimeOffset creeLe, CancellationToken ct = default);

    /// <summary>
    /// Définit une nouvelle valeur versionnée pour un attribut d'agent
    /// (<c>AgentAttributs</c> — ex. <c>NOTATION_AGENT</c>, <c>ORIGINE_STATUTAIRE</c>,
    /// <c>ANCIENNETE_PRIVEE_ANNEES</c>) : ferme la version en vigueur et insère
    /// la nouvelle. Mêmes échecs explicites que
    /// <see cref="EnregistrerEvenementCarriereAsync"/>.
    /// </summary>
    /// <returns>L'Id (GUID) de la nouvelle ligne d'attribut, en cas de succès.</returns>
    Task<Result<string>> DefinirAttributAsync(
        string agentId, string attribut, string valeur, string dateEffet, string? source,
        DateTimeOffset creeLe, CancellationToken ct = default);
}
