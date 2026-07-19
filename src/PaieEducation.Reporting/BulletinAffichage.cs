using PaieEducation.Domain.Calcul.Snapshot;

namespace PaieEducation.Reporting;

/// <summary>
/// DTO d'affichage d'un bulletin (Phase 7, 7.2b). Compose le
/// <see cref="BulletinSnapshot"/> immuable avec les éléments d'identification
/// et de cumul que le rendu PDF doit afficher mais qui n'ont pas leur place
/// dans le snapshot métier :
/// <list type="bullet">
///   <item><see cref="BulletinId"/> : GUID unique retourné par
///         <c>BulletinRepository.ValiderAsync</c> — imprimé en en-tête.</item>
///   <item><see cref="Cumuls"/> : totaux annuels depuis le 1er janvier
///         (facultatif — non affiché si <c>null</c>).</item>
/// </list>
/// Volontairement local à <c>PaieEducation.Reporting</c> : c'est une vue
/// métier du bulletin, pas un concept du domaine (D9 / ADR-0007).
/// </summary>
public sealed record BulletinAffichage(
    BulletinSnapshot Snapshot,
    string BulletinId,
    CumulsAnnuels? Cumuls = null)
{
    /// <summary>
    /// Fabrique pour les chemins « snapshot seul » (V1, tests, exports
    /// unitaires). Le <see cref="BulletinId"/> est <c>""</c> et aucun
    /// cumul n'est attaché — le rendu doit gérer ces deux cas sans
    /// planter (rétrocompatibilité avec les tests 7.2a).
    /// </summary>
    public static BulletinAffichage FromSnapshot(BulletinSnapshot snapshot) =>
        new(snapshot, BulletinId: string.Empty, Cumuls: null);
}
