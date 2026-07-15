using PaieEducation.Domain.Calcul.Services;

namespace PaieEducation.Tests.Unit.Calcul;

/// <summary>Tests du tri topologique / détection de cycles des dépendances de rubriques.</summary>
public class DependencyResolverTests
{
    private static DependanceArete E(string rub, string dependDe) => new(rub, dependDe);

    [Fact]
    public void Ordonne_les_dependances_avant_les_dependants()
    {
        // PAPP dépend de TRT ; TRT dépend de TBASE. Ordre attendu : TBASE, TRT, PAPP.
        var resolver = new DependencyResolver();
        var r = resolver.Ordonner(
            new[] { "PAPP", "TRT", "TBASE" },
            new[] { E("PAPP", "TRT"), E("TRT", "TBASE") });

        Assert.True(r.IsSuccess);
        var ordre = r.Value.ToList();
        Assert.True(ordre.IndexOf("TBASE") < ordre.IndexOf("TRT"));
        Assert.True(ordre.IndexOf("TRT") < ordre.IndexOf("PAPP"));
    }

    [Fact]
    public void Sans_arete_conserve_l_ordre_d_apparition()
    {
        var resolver = new DependencyResolver();
        var r = resolver.Ordonner(new[] { "A", "B", "C" }, Array.Empty<DependanceArete>());
        Assert.True(r.IsSuccess);
        Assert.Equal(new[] { "A", "B", "C" }, r.Value);
    }

    [Fact]
    public void Detecte_un_cycle_direct()
    {
        var resolver = new DependencyResolver();
        var r = resolver.Ordonner(new[] { "A", "B" }, new[] { E("A", "B"), E("B", "A") });
        Assert.True(r.IsFailure);
        Assert.Equal("cycle", r.Error.Code);
    }

    [Fact]
    public void Detecte_un_cycle_indirect_et_rapporte_le_chemin()
    {
        var resolver = new DependencyResolver();
        var r = resolver.Ordonner(
            new[] { "A", "B", "C" },
            new[] { E("A", "B"), E("B", "C"), E("C", "A") });
        Assert.True(r.IsFailure);
        Assert.Equal("cycle", r.Error.Code);
        Assert.Contains("→", r.Error.Message);
    }

    [Fact]
    public void Rejette_une_arete_vers_une_rubrique_hors_univers()
    {
        var resolver = new DependencyResolver();
        var r = resolver.Ordonner(new[] { "A" }, new[] { E("A", "INCONNUE") });
        Assert.True(r.IsFailure);
        Assert.Equal("validation", r.Error.Code);
    }

    [Fact]
    public void Rejette_un_noeud_en_double()
    {
        var resolver = new DependencyResolver();
        var r = resolver.Ordonner(new[] { "A", "A" }, Array.Empty<DependanceArete>());
        Assert.True(r.IsFailure);
    }

    [Fact]
    public void Graphe_en_losange_est_ordonne_sans_cycle()
    {
        // D dépend de B et C ; B et C dépendent de A. A doit précéder B,C ; B,C avant D.
        var resolver = new DependencyResolver();
        var r = resolver.Ordonner(
            new[] { "D", "B", "C", "A" },
            new[] { E("D", "B"), E("D", "C"), E("B", "A"), E("C", "A") });

        Assert.True(r.IsSuccess);
        var ordre = r.Value.ToList();
        Assert.True(ordre.IndexOf("A") < ordre.IndexOf("B"));
        Assert.True(ordre.IndexOf("A") < ordre.IndexOf("C"));
        Assert.True(ordre.IndexOf("B") < ordre.IndexOf("D"));
        Assert.True(ordre.IndexOf("C") < ordre.IndexOf("D"));
    }
}
