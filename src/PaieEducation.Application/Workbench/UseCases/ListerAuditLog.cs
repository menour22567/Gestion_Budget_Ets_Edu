using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Liste les entrées récentes du journal d'audit (Phase 6, tâche 4,
/// « Audit &amp; traçabilité ») — enveloppe mince de
/// <see cref="IAuditLogRepository.ListerAsync"/>, même patron que
/// <see cref="ListerAffectationsAgent"/> (préserve la frontière
/// Presentation→Application : aucun écran ne référence un port Domain
/// directement).
/// </summary>
public sealed class ListerAuditLog
{
    private readonly IAuditLogRepository _auditLog;

    public ListerAuditLog(IAuditLogRepository auditLog)
        => _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));

    public Task<Result<IReadOnlyList<EntreeAuditLog>>> ExecuterAsync(CancellationToken ct = default)
        => _auditLog.ListerAsync(ct);

    /// <summary>Variante filtrée et paginée (chantier P4) — même enveloppe mince.</summary>
    public Task<Result<IReadOnlyList<EntreeAuditLog>>> ExecuterAsync(FiltreAuditLog filtre, CancellationToken ct = default)
        => _auditLog.ListerAsync(filtre, ct);
}
