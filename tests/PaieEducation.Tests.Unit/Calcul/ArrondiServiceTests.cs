using PaieEducation.Domain.Calcul.Services;

namespace PaieEducation.Tests.Unit.Calcul;

/// <summary>Tests du service d'arrondi centralisé (RM-120, Q9b).</summary>
public class ArrondiServiceTests
{
    [Theory]
    [InlineData(1234.49, 1234)]
    [InlineData(1234.50, 1235)]   // arrondi commercial : 0,5 monte
    [InlineData(1234.51, 1235)]
    [InlineData(-0.5, -1)]        // away from zero
    public void DinarPlusProche_arrondit_au_dinar(decimal entree, decimal attendu)
    {
        var svc = new ArrondiService(ModeArrondi.DinarPlusProche);
        Assert.Equal(attendu, svc.Arrondir(entree));
    }

    [Theory]
    [InlineData(1234, 1230)]
    [InlineData(1235, 1240)]
    [InlineData(1236, 1240)]
    public void Dizaine_arrondit_a_la_dizaine(decimal entree, decimal attendu)
    {
        var svc = new ArrondiService(ModeArrondi.Dizaine);
        Assert.Equal(attendu, svc.Arrondir(entree));
    }

    [Fact]
    public void Centime_arrondit_a_deux_decimales()
    {
        var svc = new ArrondiService(ModeArrondi.Centime);
        Assert.Equal(1234.57m, svc.Arrondir(1234.565m));
    }

    [Fact]
    public void Defaut_est_dinar_plus_proche()
    {
        Assert.Equal(ModeArrondi.DinarPlusProche, new ArrondiService().Mode);
    }

    [Theory]
    [InlineData("DINAR_PLUS_PROCHE", ModeArrondi.DinarPlusProche)]
    [InlineData("dizaine", ModeArrondi.Dizaine)]
    [InlineData(" CENTIME ", ModeArrondi.Centime)]
    public void ParserMode_lit_le_parametre(string valeur, ModeArrondi attendu)
    {
        var r = ArrondiService.ParserMode(valeur);
        Assert.True(r.IsSuccess);
        Assert.Equal(attendu, r.Value);
    }

    [Fact]
    public void ParserMode_rejette_un_mode_inconnu()
    {
        Assert.True(ArrondiService.ParserMode("MODE_BIDON").IsFailure);
    }
}
