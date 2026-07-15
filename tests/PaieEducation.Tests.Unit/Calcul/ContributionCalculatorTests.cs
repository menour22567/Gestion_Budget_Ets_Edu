using PaieEducation.Domain.Calcul.Cotisations;

namespace PaieEducation.Tests.Unit.Calcul;

/// <summary>Tests du ContributionCalculator (cotisations paramétrables, Q3/Q3b).</summary>
public class ContributionCalculatorTests
{
    private static readonly ContributionCalculator Calc = new();

    [Fact]
    public void SS_9_pourcent_sur_assiette_cotisable()
    {
        var ss = new CotisationDef("SS", ReferenceAssiette.AssietteCotisable, Taux: 0.09m, MontantFixe: null);
        var r = Calc.Calculer(ss, assietteResolue: 50000m);
        Assert.True(r.IsSuccess);
        Assert.Equal(4500m, r.Value);
    }

    [Fact]
    public void Montant_fixe_ignore_l_assiette()
    {
        var mut = new CotisationDef("MUTUELLE", ReferenceAssiette.MontantFixe, Taux: null, MontantFixe: 500m);
        var r = Calc.Calculer(mut, assietteResolue: 999999m);
        Assert.True(r.IsSuccess);
        Assert.Equal(500m, r.Value);
    }

    [Fact]
    public void Montant_fixe_absent_echoue()
    {
        var mut = new CotisationDef("MUTUELLE", ReferenceAssiette.MontantFixe, Taux: null, MontantFixe: null);
        Assert.True(Calc.Calculer(mut, 0m).IsFailure);
    }

    [Fact]
    public void Proportionnelle_sans_taux_echoue()
    {
        var ss = new CotisationDef("SS", ReferenceAssiette.AssietteCotisable, Taux: null, MontantFixe: null);
        Assert.True(Calc.Calculer(ss, 50000m).IsFailure);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.5)]
    public void Taux_hors_intervalle_echoue(decimal taux)
    {
        var c = new CotisationDef("X", ReferenceAssiette.AssietteCotisable, taux, null);
        Assert.True(Calc.Calculer(c, 1000m).IsFailure);
    }

    [Fact]
    public void Assiette_negative_echoue()
    {
        var ss = new CotisationDef("SS", ReferenceAssiette.AssietteCotisable, 0.09m, null);
        Assert.True(Calc.Calculer(ss, -1m).IsFailure);
    }
}
