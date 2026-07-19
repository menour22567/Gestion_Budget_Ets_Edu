using PaieEducation.Domain.Calcul.Rappels;
using PaieEducation.Domain.Calcul.Snapshot;

namespace PaieEducation.Reporting;

/// <summary>
/// Port de rendu d'un document à partir d'un <see cref="BulletinSnapshot"/>
/// figé (jamais recalculé). Le rendu doit être déterministe : les montants
/// imprimés sont ceux du snapshot, pas d'une réévaluation.
/// </summary>
public interface IDocumentRenderer
{
    /// <summary>
    /// Surcharge « snapshot seul » (V1, tests, exports unitaires). Le
    /// <see cref="BulletinAffichage"/> est synthétisé sans BulletinId ni
    /// cumuls — le rendu ne doit pas planter dans ce cas (rétrocompat
    /// avec les tests 7.2a).
    /// </summary>
    byte[] Rendre(BulletinSnapshot snapshot, IReadOnlyList<LigneRappel>? rappels = null);

    /// <summary>
    /// Surcharge « affichage complet » (V2, 7.2b) : le rendu utilise le
    /// <see cref="BulletinAffichage"/> pour imprimer le BulletinId, la
    /// période en français, les cumuls annuels (si fournis) et les
    /// mentions réglementaires.
    /// <paramref name="rappels"/> optionnel : lignes additionnelles issues
    /// d'une évolution réglementaire rétroactive (D9) ; null = aucune
    /// ligne à afficher (la section « Rappels » reste présente mais vide).
    /// </summary>
    byte[] Rendre(BulletinAffichage affichage, IReadOnlyList<LigneRappel>? rappels = null);
}
