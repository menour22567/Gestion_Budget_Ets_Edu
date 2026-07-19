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
    /// Rend le bulletin au format demandé et retourne les octets du document.
    /// <paramref name="rappels"/> optionnel : lignes additionnelles issues
    /// d'une évolution réglementaire rétroactive (D9) ; null = aucune
    /// ligne à afficher (la section « Rappels » reste présente mais vide).
    /// </summary>
    byte[] Rendre(BulletinSnapshot snapshot, IReadOnlyList<LigneRappel>? rappels = null);
}
