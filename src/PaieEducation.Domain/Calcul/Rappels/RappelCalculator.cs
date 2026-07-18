using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Shared.Money;

namespace PaieEducation.Domain.Calcul.Rappels;

public sealed record LigneRappel(
    string RubriqueId,
    Money MontantAncien,
    Money MontantNouveau,
    Money Delta);

public sealed class RappelCalculator
{
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
            var montantAncien = montantsAnciens.GetValueOrDefault(id, Money.Zero);
            var montantNouveau = montantsNouveaux.GetValueOrDefault(id, Money.Zero);
            var delta = montantNouveau - montantAncien;
            if (delta != Money.Zero)
            {
                lignes.Add(new LigneRappel(id, montantAncien, montantNouveau, delta));
            }
        }
        return lignes;
    }
}
