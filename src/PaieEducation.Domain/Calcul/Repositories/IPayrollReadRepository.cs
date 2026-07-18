using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;

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

    /// <summary>
    /// Variante « what-if » pour simulation d'évolution réglementaire (D8,
    /// ADR-0007) : surcharge une partie des barèmes (table
    /// <c>RubriqueBaremes</c>) par <paramref name="baremesOverride"/> sans
    /// modifier la base. Les barèmes surchargés sont agrégés à la liste
    /// chargée depuis la DB, et le <c>BaremeResolver</c> (inchangé) les
    /// résout comme d'habitude — la première occurrence (override inséré en
    /// tête) gagne sur l'égalité (RubriqueId, Dimension, BorneInf, période).
    /// Cf. J5M §3 (D-B1 à D-B4) — Chantier 3 / Lot 3.2, extension du
    /// simulateur d'impact réel aux barèmes forfaitaires (P4, P5, P6, P12).
    /// </summary>
    /// <param name="baremesOverride">
    /// Liste de <see cref="BaremeValue"/> hypothétiques à substituer à la DB
    /// pour cette simulation. <c>null</c> = pas d'override (équivalent à
    /// <see cref="ChargerAsync"/>). Les barèmes DB restent chargés et
    /// résolus normalement pour les clés non surchargées.
    /// </param>
    Task<Result<PayrollInput>> ChargerAvecBaremesOverrideAsync(
        AgentContext agent,
        string datePaie,
        IReadOnlyDictionary<string, decimal> variablesBase,
        IReadOnlyDictionary<string, decimal> sourcesValeur,
        IReadOnlyDictionary<string, string> clesBareme,
        ProfilFiscal profil,
        IReadOnlyList<BaremeValue>? baremesOverride,
        CancellationToken ct = default);
}
