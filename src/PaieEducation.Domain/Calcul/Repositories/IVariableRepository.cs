using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Workbench.Services;

namespace PaieEducation.Domain.Calcul.Repositories;

/// <summary>
/// Résout les variables de base d'un bulletin (<c>INDICE_MIN</c>,
/// <c>INDICE_ECH</c>, <c>VPI</c>, <c>TBASE</c>, <c>TRT</c>, <c>ECH</c>,
/// <c>CAT</c>) pour un agent à une date de paie donnée. Port du Domain
/// implémenté par <c>Infrastructure.Repositories.Payroll.VariableRepository</c>.
/// </summary>
public interface IVariableRepository
{
    Task<Result<IReadOnlyDictionary<string, decimal>>> ResoudreAsync(
        AgentContext agent, string datePaie, CancellationToken ct = default);

    /// <summary>
    /// Variante « what-if » pour simulation d'évolution réglementaire (D8,
    /// ADR-0007) : surcharge la VPI par <paramref name="vpiOverride"/> sans
    /// modifier la base. Tous les autres paramètres (<c>INDICE_MIN</c>,
    /// <c>INDICE_ECH</c>) restent lus depuis la base. Cf. J5L §3.2 (D-S2) :
    /// méthode séparée plutôt que paramètre optionnel sur
    /// <see cref="ResoudreAsync"/> pour porter la sémantique « simulation »
    /// dans le nom et éviter tout breaking change sur les mocks.
    /// </summary>
    /// <param name="vpiOverride">VPI hypothétique à utiliser pour la simulation. Doit être &gt; 0.</param>
    Task<Result<IReadOnlyDictionary<string, decimal>>> ResoudreAvecVPIAsync(
        AgentContext agent, string datePaie, decimal vpiOverride, CancellationToken ct = default);
}
