using PaieEducation.Domain.Common;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Domain.Workbench.Repositories;

/// <summary>
/// Écrit et liste les lignes d'audit (<c>AuditLog</c>, V001) — journal de
/// toute action métier sensible. Port du Domain implémenté par
/// <c>Infrastructure.Repositories.Workbench.AuditLogRepository</c>.
/// </summary>
/// <remarks>
/// Le projet n'a aucune notion d'identité utilisateur courant (pas
/// d'authentification, de session, ni d'<c>IUserContext</c>) — <c>actor</c>
/// reste fourni par l'appelant, comme <c>SourcesValeur</c>/<c>ClesBareme</c>
/// dans <c>CalculerBulletin</c>.
/// </remarks>
public interface IAuditLogRepository
{
    Task<Result> EnregistrerAsync(
        string actor,
        string action,
        string entityType,
        string? entityId,
        string? payload,
        string? comment,
        DateTimeOffset occurredAt,
        CancellationToken ct = default,
        IUnitOfWork? uow = null);

    /// <summary>
    /// Les 500 entrées les plus récentes, triées par <c>OccurredAt</c>
    /// décroissant — pas de pagination réelle (dette assumée, cf. mémoire
    /// phase6-audit-log-ecran), un plafond simple suffit pour une vue
    /// d'audit consultée occasionnellement.
    /// </summary>
    Task<Result<IReadOnlyList<EntreeAuditLog>>> ListerAsync(CancellationToken ct = default);
}
