using PaieEducation.Domain.Calcul.ValueObjects;

namespace PaieEducation.Tests.Unit.Calcul;

/// <summary>
/// Tests de <see cref="Fraction"/> — les fractions exactes de lissage IRG (V007).
/// L'enjeu : « 8/3 » ne se représente pas en double ; la fraction doit rester
/// exacte jusqu'à l'arrondi final.
/// </summary>
public class FractionTests
{
    [Theory]
    [InlineData("8/3", 8, 3)]
    [InlineData("137/51", 137, 51)]
    [InlineData("20000/3", 20000, 3)]
    [InlineData("30000", 30000, 1)]
    [InlineData(" 5 / 3 ", 5, 3)]
    public void Parser_accepte_fractions_et_entiers(string texte, long num, long den)
    {
        var r = Fraction.Parser(texte);
        Assert.True(r.IsSuccess);
        Assert.Equal(num, r.Value.Numerateur);
        Assert.Equal(den, r.Value.Denominateur);
    }

    [Fact]
    public void Creer_reduit_la_fraction()
    {
        var f = Fraction.Creer(20000, 10000);
        Assert.Equal(2, f.Numerateur);
        Assert.Equal(1, f.Denominateur);
    }

    [Fact]
    public void Creer_normalise_le_signe_au_numerateur()
    {
        var f = Fraction.Creer(3, -4);
        Assert.Equal(-3, f.Numerateur);
        Assert.Equal(4, f.Denominateur);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("8/0")]
    [InlineData("abc")]
    [InlineData("3/x")]
    public void Parser_rejette_les_entrees_malformees(string texte)
    {
        Assert.True(Fraction.Parser(texte).IsFailure);
    }

    [Fact]
    public void Creer_denominateur_nul_leve()
    {
        Assert.Throws<ArgumentException>(() => Fraction.Creer(1, 0));
    }

    [Fact]
    public void Multiplier_applique_le_numerateur_avant_la_division()
    {
        // 51 × 137/51 = 137 exactement (pas 136,999… d'un double).
        var f = Fraction.Parser("137/51").Value;
        Assert.Equal(137m, f.Multiplier(51m));
    }

    [Fact]
    public void Lissage_general_2022_est_exact_a_l_arrondi()
    {
        // Lissage général 2022+ : SI × 137/51 − 27925/8.
        // Pour SI = 34 000 : 34000×137/51 − 27925/8
        //   = 4658000/51 − 27925/8 = 91333,333… − 3490,625 = 87842,708…
        var coef = Fraction.Parser("137/51").Value;
        var cst = Fraction.Parser("27925/8").Value;
        var v = coef.Multiplier(34000m) - cst.VersDecimal();
        Assert.Equal(87842.71m, Math.Round(v, 2, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void ToString_rend_la_forme_canonique()
    {
        Assert.Equal("8/3", Fraction.Parser("16/6").Value.ToString());
        Assert.Equal("30000", Fraction.Parser("30000").Value.ToString());
    }
}
