using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Domain.Workbench.Services;

/// <summary>
/// Implémentation par défaut de <see cref="IBaremeResolver"/>. Sélectionne la
/// ligne la plus récente dont la période englobe la date demandée et dont la
/// clé de tranche couvre la clé demandée. Pure — pas d'I/O, pas d'horloge.
/// </summary>
public sealed class BaremeResolver : IBaremeResolver
{
    /// <inheritdoc />
    public BaremeValue? Resoudre(
        string rubriqueId,
        BaremeDimension dimension,
        string cle,
        string datePaie,
        IReadOnlyList<BaremeValue> baremes)
    {
        ArgumentNullException.ThrowIfNull(baremes);

        BaremeValue? meilleur = null;
        foreach (var b in baremes)
        {
            if (b.RubriqueId != rubriqueId) continue;
            if (b.Dimension != dimension) continue;
            if (!b.Couvre(cle)) continue;
            if (!b.Periode.Contient(datePaie)) continue;
            // On garde le plus récent (DateEffet la plus grande).
            if (meilleur is null
                || string.CompareOrdinal(b.Periode.DateEffet, meilleur.Periode.DateEffet) > 0)
            {
                meilleur = b;
            }
        }
        return meilleur;
    }
}
