using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Snapshot;

namespace PaieEducation.Domain.Calcul.Rappels;

/// <summary>
/// Une ligne de rappel : différence entre le montant payé à l'époque (snapshot)
/// et le montant recalculé à droit constant de la période de référence.
/// </summary>
public sealed record LigneRappel(
    string RubriqueId,
    decimal MontantAncien,
    decimal MontantNouveau,
    decimal Delta);

/// <summary>
/// Calcule les lignes de rappel entre un snapshot d'époque et un recalcul
/// (J3C §11, RM-102 ; ADR-0008). Primitive pure : ne détermine jamais quels
/// bulletins validés sont impactés par un changement rétroactif — c'est la
/// responsabilité du use case applicatif <c>GenererRappels</c> (Phase 5,
/// `PLAN_ACTION.md` Phase 5 §5), qui appelle cette primitive par bulletin
/// impacté. Un bulletin clôturé n'est jamais recalculé (ADR-0008) : le rappel
/// est la seule ligne additionnelle, jamais une modification du snapshot.
/// </summary>
public sealed class RappelCalculator
{
    /// <summary>
    /// Compare <paramref name="ancien"/> (snapshot payé) à <paramref name="nouveau"/>
    /// (recalcul à droit constant de la même période) et renvoie une ligne de
    /// rappel par rubrique dont le montant diffère. Une rubrique présente d'un
    /// seul côté est traitée comme un montant nul de l'autre (nouvelle
    /// éligibilité ⇒ rappel positif ; éligibilité perdue ⇒ rappel négatif).
    /// </summary>
    public IReadOnlyList<LigneRappel> Calculer(BulletinSnapshot ancien, Bulletin nouveau)
    {
        ArgumentNullException.ThrowIfNull(ancien);
        ArgumentNullException.ThrowIfNull(nouveau);

        var montantsAnciens = ancien.Resultat.Lignes.ToDictionary(l => l.RubriqueId, l => l.Montant, StringComparer.Ordinal);
        var montantsNouveaux = nouveau.Lignes.ToDictionary(l => l.RubriqueId, l => l.Montant, StringComparer.Ordinal);

        var rubriques = montantsAnciens.Keys.Union(montantsNouveaux.Keys, StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal);

        var lignes = new List<LigneRappel>();
        foreach (var id in rubriques)
        {
            var montantAncien = montantsAnciens.GetValueOrDefault(id, 0m);
            var montantNouveau = montantsNouveaux.GetValueOrDefault(id, 0m);
            var delta = montantNouveau - montantAncien;
            if (delta != 0m)
            {
                lignes.Add(new LigneRappel(id, montantAncien, montantNouveau, delta));
            }
        }
        return lignes;
    }
}
