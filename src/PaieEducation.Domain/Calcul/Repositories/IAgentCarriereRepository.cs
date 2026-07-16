using PaieEducation.Domain.Common;
using PaieEducation.Domain.Workbench.Services;

namespace PaieEducation.Domain.Calcul.Repositories;

/// <summary>
/// Résout un <see cref="AgentContext"/> pour un agent à une date de paie donnée.
/// Port du Domain implémenté par <c>Infrastructure.Repositories.Agents.AgentCarriereRepository</c>
/// (I/O réelle — inaccessible depuis <c>Application</c>, cf.
/// <c>DependencyRulesTests.Application_ne_depend_ni_de_infrastructure_ni_de_persistence</c>).
/// </summary>
public interface IAgentCarriereRepository
{
    Task<Result<AgentContext>> ResoudreAsync(string agentId, string datePaie, CancellationToken ct = default);
}
