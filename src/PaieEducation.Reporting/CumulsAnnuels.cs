using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Shared.Money;

namespace PaieEducation.Reporting;

/// <summary>
/// Cumuls annuels d'un agent sur une année civile (Phase 7, 7.2b).
/// Affichés dans la section « Cumuls depuis le 1er janvier » du bulletin
/// PDF — information obligatoire pour le suivi individuel de la paie.
/// </summary>
/// <param name="Annee">Année civile (AAAA) des cumuls.</param>
/// <param name="NombreBulletins">Nombre de bulletins validés pris en compte.</param>
/// <param name="TotalGains">Somme des gains sur l'année.</param>
/// <param name="TotalImposable">Somme des assiettes imposables.</param>
/// <param name="TotalCotisable">Somme des assiettes cotisables.</param>
/// <param name="TotalRetenues">Somme des retenues (cotisations salariales + autres).</param>
/// <param name="TotalIrg">Somme de l'IRG prélevé.</param>
/// <param name="TotalNet">Somme des nets à payer.</param>
public sealed record CumulsAnnuels(
    int Annee,
    int NombreBulletins,
    Money TotalGains,
    Money TotalImposable,
    Money TotalCotisable,
    Money TotalRetenues,
    Money TotalIrg,
    Money TotalNet)
{
    /// <summary>Cumuls à zéro pour une année sans bulletin validé (affichage neutre).</summary>
    public static CumulsAnnuels Vide(int annee) => new(
        Annee: annee,
        NombreBulletins: 0,
        TotalGains: Money.Zero,
        TotalImposable: Money.Zero,
        TotalCotisable: Money.Zero,
        TotalRetenues: Money.Zero,
        TotalIrg: Money.Zero,
        TotalNet: Money.Zero);

    /// <summary>
    /// Agrège une liste de bulletins en cumuls annuels. Les bulletins sont
    /// groupés par année civile (extraite de <c>Bulletin.Input.DatePaie</c>)
    /// et sommés sur les six totaux. Le <see cref="Money"/> est garanti
    /// non-null (un bulletin validé a toujours un net calculé).
    /// </summary>
    public static CumulsAnnuels FromBulletins(int annee, IReadOnlyList<Bulletin> bulletins)
    {
        ArgumentNullException.ThrowIfNull(bulletins);
        if (bulletins.Count == 0) return Vide(annee);

        var gains = Money.Zero;
        var imposable = Money.Zero;
        var cotisable = Money.Zero;
        var retenues = Money.Zero;
        var irg = Money.Zero;
        var net = Money.Zero;

        foreach (var b in bulletins)
        {
            gains += b.TotalGains;
            imposable += b.AssietteImposable;
            cotisable += b.AssietteCotisable;
            retenues += b.TotalRetenues;
            irg += b.Irg;
            net += b.Net;
        }

        return new CumulsAnnuels(annee, bulletins.Count, gains, imposable, cotisable, retenues, irg, net);
    }
}
