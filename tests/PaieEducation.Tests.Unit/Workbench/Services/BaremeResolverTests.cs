using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Tests.Unit.Workbench.Services;

/// <summary>
/// Tests de <see cref="BaremeResolver"/>. Logique de résolution :
/// (rubrique, dimension, clé) → valeur active à la date demandée.
/// </summary>
public class BaremeResolverTests
{
    private static BaremeValue B(string rub, BaremeDimension dim, string inf, string? sup,
                                  BaremeTypeValeur type, string val, string eff, string? fin = null)
        => BaremeValue.Creer(rub, dim, inf, sup, type, val, PeriodeReglementaire.Creer(eff, fin));

    [Fact]
    public void Resoudre_retourne_null_si_aucun_bareme_pour_la_cle()
    {
        var resolver = new BaremeResolver();
        var r = resolver.Resoudre("QUALIF", BaremeDimension.Categorie, "99", "2025-06-15",
            new[] { B("QUALIF", BaremeDimension.Categorie, "1", "10", BaremeTypeValeur.Taux, "0.40", "2008-01-01") });
        Assert.Null(r);
    }

    [Fact]
    public void Resoudre_selectionne_le_bareme_actif_a_la_date_demandee()
    {
        var resolver = new BaremeResolver();
        var r = resolver.Resoudre("QUALIF", BaremeDimension.Categorie, "8", "2025-06-15", new[]
        {
            B("QUALIF", BaremeDimension.Categorie, "1", "12", BaremeTypeValeur.Taux, "0.40", "2008-01-01", "2010-12-31"),
            B("QUALIF", BaremeDimension.Categorie, "1", "12", BaremeTypeValeur.Taux, "0.45", "2011-01-01", "2024-12-31"),
            B("QUALIF", BaremeDimension.Categorie, "1", "12", BaremeTypeValeur.Taux, "0.50", "2025-01-01", null),
        });
        Assert.NotNull(r);
        Assert.Equal("0.50", r!.Valeur);   // la plus récente active à 2025-06-15
    }

    [Fact]
    public void Resoudre_ignore_les_barres_d_une_autre_rubrique()
    {
        var resolver = new BaremeResolver();
        var r = resolver.Resoudre("DOC", BaremeDimension.Categorie, "8", "2025-06-15", new[]
        {
            B("QUALIF", BaremeDimension.Categorie, "1", "12", BaremeTypeValeur.Taux, "0.40", "2025-01-01", null),
            B("DOC",    BaremeDimension.Categorie, "1", "10", BaremeTypeValeur.Montant, "2000", "2025-01-01", null),
        });
        Assert.NotNull(r);
        Assert.Equal("2000", r!.Valeur);
    }

    [Fact]
    public void Resoudre_ignore_les_barres_d_une_autre_dimension()
    {
        var resolver = new BaremeResolver();
        var r = resolver.Resoudre("DOC", BaremeDimension.Categorie, "5", "2025-06-15", new[]
        {
            B("DOC", BaremeDimension.TypeEtablissement, "PRIMAIRE", null, BaremeTypeValeur.Montant, "3000", "2025-01-01", null),
            B("DOC", BaremeDimension.Categorie, "1", "10", BaremeTypeValeur.Montant, "2000", "2025-01-01", null),
        });
        Assert.NotNull(r);
        Assert.Equal("2000", r!.Valeur);
    }

    [Fact]
    public void Resoudre_respecte_la_cle_de_tranche()
    {
        // Barème IFC par tranche de catégorie — chaque tranche a sa propre période.
        var resolver = new BaremeResolver();
        var r = resolver.Resoudre("IFC", BaremeDimension.Categorie, "7", "2025-06-15", new[]
        {
            B("IFC", BaremeDimension.Categorie, "1",  "6",  BaremeTypeValeur.Montant, "3200", "2025-01-01", null),
            B("IFC", BaremeDimension.Categorie, "7",  "8",  BaremeTypeValeur.Montant, "2500", "2025-01-01", null),
            B("IFC", BaremeDimension.Categorie, "9",  "10", BaremeTypeValeur.Montant, "2000", "2025-01-01", null),
            B("IFC", BaremeDimension.Categorie, "11", "17", BaremeTypeValeur.Montant, "1500", "2025-01-01", null),
        });
        Assert.Equal("2500", r!.Valeur);
    }
}
