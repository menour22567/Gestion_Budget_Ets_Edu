namespace PaieEducation.Domain.Workbench.ValueObjects;

/// <summary>
/// Critères de filtrage et de pagination pour la lecture du journal d'audit
/// (chantier P4 — lève le plafond fixe <c>LIMIT 500</c> de
/// <c>IAuditLogRepository.ListerAsync()</c>). Tous les filtres sont
/// optionnels et combinés en ET ; aucun filtre = comportement historique
/// (dernières entrées d'abord).
/// </summary>
/// <param name="Actor">Acteur exact (<c>AuditLog.Actor</c>), ou <c>null</c> = tous.</param>
/// <param name="Action">Action exacte (<see cref="Constants.AuditActions"/>), ou <c>null</c> = toutes.</param>
/// <param name="EntityType">Type d'entité exact (<see cref="Constants.AuditEntityTypes"/>), ou <c>null</c> = tous.</param>
/// <param name="DateDebut">Borne inférieure inclusive sur <c>OccurredAt</c> (UTC), ou <c>null</c>.</param>
/// <param name="DateFin">Borne supérieure inclusive sur <c>OccurredAt</c> (UTC), ou <c>null</c>.</param>
/// <param name="Page">Page 1-indexée.</param>
/// <param name="TaillePage">Taille de page, doit être dans [1, <see cref="TaillePageMax"/>].</param>
public sealed record FiltreAuditLog(
    string? Actor = null,
    string? Action = null,
    string? EntityType = null,
    DateTimeOffset? DateDebut = null,
    DateTimeOffset? DateFin = null,
    int Page = 1,
    int TaillePage = FiltreAuditLog.TaillePageParDefaut)
{
    /// <summary>Taille de page par défaut — utilisée par la pagination incrémentale de l'écran.</summary>
    public const int TaillePageParDefaut = 100;

    /// <summary>
    /// Taille de page maximale autorisée — aussi la taille utilisée par
    /// l'ancien comportement non filtré (<c>ListerAsync(CancellationToken)</c>),
    /// conservé pour compatibilité (ex-<c>LIMIT 500</c> en dur).
    /// </summary>
    public const int TaillePageMax = 500;
}
