using PaieEducation.Domain.Common;

namespace PaieEducation.Domain.Workbench.Repositories;

/// <summary>
/// Écrit une ligne d'audit (<c>AuditLog</c>, V001) — journal de toute action
/// métier sensible. Port du Domain implémenté par
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
        CancellationToken ct = default);
}
