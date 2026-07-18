using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.ValueObjects;

namespace PaieEducation.Tests.Unit.Calcul;

/// <summary>
/// Tests de l'IrgCalculator contre le pseudo-code de référence
/// (CALCUL IRG ALGERIE.txt). Cas de référence verrouillés + bornes des lissages.
/// </summary>
public class IrgCalculatorTests
{
    // Valeurs par défaut (seedées dans Parametres, C8.1).
    private const decimal SeuilExoneration = 30000m;
    private const decimal PlafondLissageGeneral = 35000m;

    private static readonly IrgTranche[] Bareme2008 =
    {
        new(0, 10000, 0.00m),
        new(10001, 30000, 0.20m),
        new(30001, 120000, 0.30m),
        new(120001, null, 0.35m),
    };

    private static readonly IrgTranche[] Bareme2022 =
    {
        new(0, 20000, 0.00m),
        new(20001, 40000, 0.23m),
        new(40001, 80000, 0.27m),
        new(80001, 160000, 0.30m),
        new(160001, 320000, 0.33m),
        new(320001, null, 0.35m),
    };

    private static Fraction F(string s) => Fraction.Parser(s).Value;

    private static IrgReglePeriode Periode2022() => new(
        "IRG-PER-2022", ExonerationSeuil: 30000, AbattementTaux: 0.40m,
        AbattementMin: 1000, AbattementMax: 1500,
        CoefGeneral: F("137/51"), ConstGeneral: F("27925/8"),
        CoefSpecial: F("93/61"), ConstSpecial: F("81213/41"),
        PlafondSpecial: 42500, Tranches: Bareme2022);

    private static IrgReglePeriode Periode2020() => new(
        "IRG-PER-2020-06", ExonerationSeuil: 30000, AbattementTaux: 0.40m,
        AbattementMin: 1000, AbattementMax: 1500,
        CoefGeneral: F("8/3"), ConstGeneral: F("20000/3"),
        CoefSpecial: F("5/3"), ConstSpecial: F("12500/3"),
        PlafondSpecial: 40000, Tranches: Bareme2008);

    private static IrgReglePeriode PeriodeAvant2020() => new(
        "IRG-PER-AV-2020-06", ExonerationSeuil: 0, AbattementTaux: 0.40m,
        AbattementMin: 1000, AbattementMax: 1500,
        CoefGeneral: Fraction.Un, ConstGeneral: Fraction.Zero,
        CoefSpecial: Fraction.Un, ConstSpecial: Fraction.Zero,
        PlafondSpecial: 0, Tranches: Bareme2008);

    // ---- IRG brut (cas de référence verrouillé) ----

    [Fact]
    public void Brut_reference_54800_barème_2022_vaut_8596()
    {
        Assert.Equal(8596m, IrgCalculator.CalculerBrut(54800m, Bareme2022));
    }

    [Fact]
    public void Brut_reference_54800_barème_2008_vaut_11440()
    {
        Assert.Equal(11440m, IrgCalculator.CalculerBrut(54800m, Bareme2008));
    }

    [Theory]
    [InlineData(10000, 0)]        // sous la 1re tranche imposable
    [InlineData(30000, 4000)]     // 2008 : 20000 × 20 %
    [InlineData(120000, 31000)]   // 2008 : 4000 + 90000×30 %
    public void Brut_2008_par_palier(decimal revenu, decimal attendu)
    {
        Assert.Equal(attendu, IrgCalculator.CalculerBrut(revenu, Bareme2008));
    }

    // ---- Algorithme complet ----

    [Fact]
    public void Exoneration_revenu_sous_30000_donne_zero()
    {
        var r = new IrgCalculator(SeuilExoneration, PlafondLissageGeneral).Calculer(28000m, ProfilFiscal.Standard, Periode2022());
        Assert.True(r.IsSuccess);
        Assert.Equal(0m, r.Value.Final);
        Assert.Equal("exoneration", r.Value.EtapeAppliquee);
    }

    [Fact]
    public void Standard_hors_bande_applique_abattement_plafonne()
    {
        // 54 800 : brut 8596, abattement 40 %=3438,4 → plafonné 1500 → 7096.
        var r = new IrgCalculator(SeuilExoneration, PlafondLissageGeneral).Calculer(54800m, ProfilFiscal.Standard, Periode2022());
        Assert.True(r.IsSuccess);
        Assert.Equal(8596m, r.Value.Brut);
        Assert.Equal(1500m, r.Value.Abattement);
        Assert.Equal(7096m, r.Value.Final);
        Assert.Equal("standard", r.Value.EtapeAppliquee);
    }

    [Fact]
    public void Lissage_general_dans_la_bande_30000_35000()
    {
        // 2020, SI = 32 000 : brut 2008 = (30000-10000)×0,20 + (32000-30000)×0,30
        //   = 4000 + 600 = 4600 ; abattement 40 %=1840 → plafonné 1500 → apres = 3100.
        // Lissage général : 3100 × 8/3 − 20000/3 = 24800/3 − 20000/3 = 4800/3 = 1600.
        var r = new IrgCalculator(SeuilExoneration, PlafondLissageGeneral).Calculer(32000m, ProfilFiscal.Standard, Periode2020());
        Assert.True(r.IsSuccess);
        Assert.Equal("lissage_general", r.Value.EtapeAppliquee);
        Assert.Equal(1600m, Math.Round(r.Value.Final, 2));
    }

    [Fact]
    public void Lissage_special_prioritaire_pour_profil_handicape()
    {
        // 2020, SI = 38 000 (dans [30000 ; 40000] plafond spécial, hors bande générale).
        // brut 2008 = 4000 + (38000-30000)×0,30 = 4000+2400 = 6400 ;
        // abattement plafonné 1500 → apres = 4900.
        // Lissage spécial : 4900 × 5/3 − 12500/3 = 24500/3 − 12500/3 = 12000/3 = 4000.
        var r = new IrgCalculator(SeuilExoneration, PlafondLissageGeneral).Calculer(38000m, ProfilFiscal.HandicapeOuRetraiteRG, Periode2020());
        Assert.True(r.IsSuccess);
        Assert.Equal("lissage_special", r.Value.EtapeAppliquee);
        Assert.Equal(4000m, Math.Round(r.Value.Final, 2));
    }

    [Fact]
    public void Profil_standard_n_a_pas_le_lissage_special()
    {
        // Même SI = 38 000 mais profil standard : hors bande générale (>35000) →
        // étape standard (pas de lissage).
        var r = new IrgCalculator(SeuilExoneration, PlafondLissageGeneral).Calculer(38000m, ProfilFiscal.Standard, Periode2020());
        Assert.True(r.IsSuccess);
        Assert.Equal("standard", r.Value.EtapeAppliquee);
    }

    [Fact]
    public void Avant_2020_pas_d_exoneration_ni_lissage()
    {
        // SI = 32 000 avant 2020-06 : pas d'exonération (seuil 0), lissage identité.
        // brut = 4600 ; abattement plafonné 1500 → 3100.
        var r = new IrgCalculator(SeuilExoneration, PlafondLissageGeneral).Calculer(32000m, ProfilFiscal.Standard, PeriodeAvant2020());
        Assert.True(r.IsSuccess);
        Assert.Equal("standard", r.Value.EtapeAppliquee);
        Assert.Equal(3100m, r.Value.Final);
    }

    [Fact]
    public void Abattement_minimum_1000_sur_petit_brut()
    {
        // brut faible : abattement = max(40%, 1000). SI=30500 (2020), brut 2008 =
        // 4000 + 500×0,30 = 4150 ; 40 %=1660 → dans [1000;1500] → plafonné 1500.
        var r = new IrgCalculator(SeuilExoneration, PlafondLissageGeneral).Calculer(30500m, ProfilFiscal.Standard, Periode2020());
        Assert.True(r.IsSuccess);
        Assert.Equal(1500m, r.Value.Abattement);
    }

    [Fact]
    public void Revenu_negatif_echoue()
    {
        Assert.True(new IrgCalculator(SeuilExoneration, PlafondLissageGeneral).Calculer(-1m, ProfilFiscal.Standard, Periode2022()).IsFailure);
    }
}
