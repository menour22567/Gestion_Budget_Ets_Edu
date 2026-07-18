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
}
