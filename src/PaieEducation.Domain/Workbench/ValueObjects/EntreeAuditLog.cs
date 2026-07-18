namespace PaieEducation.Domain.Workbench.ValueObjects;

/// <summary>
/// Une ligne du journal d'audit (<c>AuditLog</c>, V001) — projection de
/// lecture pour la vue « Audit &amp; traçabilité » (Phase 6, tâche 4).
/// </summary>
public sealed record EntreeAuditLog(
    long Id,
    string OccurredAt,
    string Actor,
    string Action,
    string EntityType,
    string? EntityId,
    string? Payload,
    string? Comment);
