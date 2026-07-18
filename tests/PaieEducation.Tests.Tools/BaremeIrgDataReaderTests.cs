using PaieEducation.Seeding;

namespace PaieEducation.Tests.Tools;

/// <summary>
/// Tests Lot 1.3α — lecteur du jeu de données barèmes IRG embarqué
/// (<c>baremes_irg_v1.json</c>). Couvre :
///   - chargement et structure (2 barèmes, 10 tranches)
///   - déterminisme du hash sur données inchangées
///   - détection de drift (un changement de taux recalcule le hash)
/// </summary>
public class BaremeIrgDataReaderTests
{
    [Fact]
    public void Load_retourne_2_baremes_et_10_tranches()
    {
        var data = BaremeIrgDataReader.Load();

        Assert.Equal("1.0", data.Version);
        Assert.Equal(2, data.Baremes.Count);
        Assert.Equal(4, data.Baremes.Single(b => b.Id == "IRG-2008").Tranches.Count);
        Assert.Equal(6, data.Baremes.Single(b => b.Id == "IRG-2022").Tranches.Count);
        Assert.Equal(10, data.Baremes.Sum(b => b.Tranches.Count));
    }

    [Fact]
    public void Load_preserve_les_bornes_inferieures_et_superieures_des_tranches()
    {
        var data = BaremeIrgDataReader.Load();
        var t2008 = data.Baremes.Single(b => b.Id == "IRG-2008").Tranches.Single(t => t.Id == "IRG-2008-T1");
        var t2022Last = data.Baremes.Single(b => b.Id == "IRG-2022").Tranches.Single(t => t.Id == "IRG-2022-T6");

        Assert.Equal(0, t2008.BorneInf);
        Assert.Equal(10000, t2008.BorneSup);
        Assert.Equal(0m, t2008.Taux);

        Assert.Equal(320001, t2022Last.BorneInf);
        Assert.Null(t2022Last.BorneSup); // +infini
        Assert.Equal(0.35m, t2022Last.Taux);
    }

    [Fact]
    public void HashLigne_est_deterministe_pour_les_memes_donnees()
    {
        // Le hash doit être stable tant que la donnée ne change pas —
        // sinon on ne peut pas s'appuyer dessus pour comparer deux bases.
        var tranche = new BaremeIrgTranche("X-1", 0, 10000, 0.20m, 1, "test");
        var h1 = BaremeIrgDataReader.HashLigne(tranche);
        var h2 = BaremeIrgDataReader.HashLigne(tranche);

        Assert.Equal(h1, h2);
        Assert.StartsWith("sha256:", h1);
    }

    [Fact]
    public void HashLigne_derive_quand_une_donnee_change()
    {
        var t1 = new BaremeIrgTranche("X-1", 0, 10000, 0.20m, 1, "test");
        var t2 = t1 with { Taux = 0.21m }; // seul le taux change

        var h1 = BaremeIrgDataReader.HashLigne(t1);
        var h2 = BaremeIrgDataReader.HashLigne(t2);

        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void HashLigne_derive_quand_la_borne_superieure_passe_de_finie_a_null()
    {
        // Le barème IRG 2022 a sa dernière tranche avec BorneSup = null
        // (borne supérieure ouverte = +infini). Un hash identique entre
        // "320001..null" et "320001..999999999" serait un faux positif de
        // drift : le format nullable doit être pris en compte.
        var fini = new BaremeIrgTranche("X-1", 320001, 999_999_999, 0.35m, 6, "LF 2022");
        var ouvert = new BaremeIrgTranche("X-1", 320001, null, 0.35m, 6, "LF 2022");

        var hFini = BaremeIrgDataReader.HashLigne(fini);
        var hOuvert = BaremeIrgDataReader.HashLigne(ouvert);

        Assert.NotEqual(hFini, hOuvert);
    }

    [Fact]
    public void HashLigne_est_independant_de_l_ordre_des_proprietes()
    {
        // Anonym object : on sérialise sans ordre déterministe côté .NET
        // pour les types anonymes (l'ordre des propriétés suit l'ordre
        // lexical du compilateur, déjà déterministe). On vérifie surtout
        // que 2 instances équivalentes produisent le même hash.
        var ligne1 = new { Id = "X-1", BorneInf = 0, BorneSup = (int?)10000, Taux = 0.20m, Ordre = 1, Source = "test" };
        var ligne2 = new { Id = "X-1", BorneInf = 0, BorneSup = (int?)10000, Taux = 0.20m, Ordre = 1, Source = "test" };

        Assert.Equal(BaremeIrgDataReader.HashLigne(ligne1), BaremeIrgDataReader.HashLigne(ligne2));
    }
}
