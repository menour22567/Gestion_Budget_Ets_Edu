using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Common;
using PaieEducation.Domain.Workbench.Services;

namespace PaieEducation.Domain.Calcul.Repositories;

/// <summary>
/// Charge le <see cref="PayrollInput"/> d'un calcul de bulletin (formules,
/// barèmes, règles d'éligibilité, cotisations, règle IRG) pour un agent à une
/// date de paie donnée. Port du Domain implémenté par
/// <c>Infrastructure.Repositories.Payroll.PayrollReadRepository</c>.
/// </summary>
public interface IPayrollReadRepository
{
    Task<Result<PayrollInput>> ChargerAsync(
        AgentContext agent,
        string datePaie,
        IReadOnlyDictionary<string, decimal> variablesBase,
        IReadOnlyDictionary<string, decimal> sourcesValeur,
        IReadOnlyDictionary<string, string> clesBareme,
        ProfilFiscal profil,
        CancellationToken ct = default);
}
