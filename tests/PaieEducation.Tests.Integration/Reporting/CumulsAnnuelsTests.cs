using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Domain.Calcul.ValueObjects;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Shared.Money;
using PaieEducation.Reporting;

namespace PaieEducation.Tests.Integration.Reporting;

/// <summary>
/// Tests unitaires du value object <see cref="CumulsAnnuels"/> (Phase 7,
/// 7.2b). L'agrégation doit sommer correctement les six totaux
/// (gains, imposable, cotisable, retenues, IRG, net) sur l'ensemble des
/// bulletins validés de l'année.
/// </summary>
public class CumulsAnnuelsTests
{
    private static Bulletin Bulletin(decimal net, decimal gains, decimal irg, decimal retenues, decimal imposable, decimal cotisable)
        => new(
            Lignes: Array.Empty<BulletinLigne>(),
            TotalGains: new Money(gains),
            AssietteCotisable: new Money(cotisable),
            AssietteImposable: new Money(imposable),
            TotalRetenues: new Money(retenues),
            Irg: new Money(irg),
            Net: new Money(net),
            Audit: new PaieEducation.Domain.Calcul.Audit.JournalAudit(Array.Empty<PaieEducation.Domain.Calcul.Audit.EtapeAudit>()));

    [Fact]
    public void Vide_retourne_des_cumuls_a_zero_pour_l_annee_demandee()
    {
        var cumuls = CumulsAnnuels.Vide(2025);

        Assert.Equal(2025, cumuls.Annee);
        Assert.Equal(0, cumuls.NombreBulletins);
        Assert.Equal(Money.Zero, cumuls.TotalGains);
        Assert.Equal(Money.Zero, cumuls.TotalNet);
    }

    [Fact]
    public void FromBulletins_avec_liste_vide_retourne_Vide()
    {
        var cumuls = CumulsAnnuels.FromBulletins(2025, Array.Empty<Bulletin>());

        Assert.Equal(0, cumuls.NombreBulletins);
        Assert.Equal(Money.Zero, cumuls.TotalNet);
    }

    [Fact]
    public void FromBulletins_agrege_tous_les_totaux_sur_l_annee()
    {
        var b1 = Bulletin(net: 50_000m, gains: 70_000m, irg: 10_000m, retenues: 20_000m, imposable: 60_000m, cotisable: 55_000m);
        var b2 = Bulletin(net: 57_739m, gains: 75_325m, irg: 10_807m, retenues: 17_586m, imposable: 68_546m, cotisable: 67_000m);
        var b3 = Bulletin(net: 60_000m, gains: 80_000m, irg: 12_000m, retenues: 20_000m, imposable: 72_000m, cotisable: 70_000m);

        var cumuls = CumulsAnnuels.FromBulletins(2025, new[] { b1, b2, b3 });

        Assert.Equal(2025, cumuls.Annee);
        Assert.Equal(3, cumuls.NombreBulletins);
        Assert.Equal(225_325m, cumuls.TotalGains.Amount);
        Assert.Equal(200_546m, cumuls.TotalImposable.Amount);
        Assert.Equal(192_000m, cumuls.TotalCotisable.Amount);
        Assert.Equal(57_586m, cumuls.TotalRetenues.Amount);
        Assert.Equal(32_807m, cumuls.TotalIrg.Amount);
        Assert.Equal(167_739m, cumuls.TotalNet.Amount);
    }

    [Fact]
    public void FromBulletins_liste_nulle_leve_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CumulsAnnuels.FromBulletins(2025, null!));
    }
}
