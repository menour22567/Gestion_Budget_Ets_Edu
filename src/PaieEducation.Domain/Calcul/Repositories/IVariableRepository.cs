using PaieEducation.Domain.Common;
using PaieEducation.Domain.Workbench.Services;

namespace PaieEducation.Domain.Calcul.Repositories;

/// <summary>
/// Résout les variables de base d'un bulletin (<c>INDICE_MIN</c>,
/// <c>INDICE_ECH</c>, <c>VPI</c>, <c>TBASE</c>, <c>TRT</c>, <c>ECH</c>, <c>CAT</c>)
/// pour un agent à une date de paie donnée. Port du Domain implémenté par
/// <c>Infrastructure.Repositories.Payroll.VariableRepository</c>.
/// </summary>
public interface IVariableRepository
{
    Task<Result<IReadOnlyDictionary<string, decimal>>> ResoudreAsync(
        AgentContext agent, string datePaie, CancellationToken ct = default);
}
