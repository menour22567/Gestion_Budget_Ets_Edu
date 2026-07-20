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
    /// Les entrées les plus récentes, triées par <c>OccurredAt</c> décroissant,
    /// plafonnées à <see cref="FiltreAuditLog.TaillePageMax"/> — comportement
    /// historique conservé pour compatibilité, délègue à
    /// <see cref="ListerAsync(FiltreAuditLog, CancellationToken)"/> sans filtre
    /// (chantier P4 : la pagination réelle vit désormais dans la surcharge
    /// filtrée).
    /// </summary>
    Task<Result<IReadOnlyList<EntreeAuditLog>>> ListerAsync(CancellationToken ct = default);

    /// <summary>
    /// Variante filtrée et paginée (chantier P4) — acteur/action/type
    /// d'entité/période, page 1-indexée. Tri déterministe
    /// (<c>OccurredAt DESC, Id DESC</c>) pour une pagination stable.
    /// </summary>
    Task<Result<IReadOnlyList<EntreeAuditLog>>> ListerAsync(FiltreAuditLog filtre, CancellationToken ct = default);
}
