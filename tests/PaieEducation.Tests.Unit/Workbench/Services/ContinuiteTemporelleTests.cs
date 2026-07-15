using PaieEducation.Application.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Shared.Results;

namespace PaieEducation.Tests.Unit.Workbench.Services;

/// <summary>
/// Tests de <see cref="ContinuiteTemporelle"/>. Couvre la validation
/// pas-de-chevauchement / pas-de-trou / une-seule-ouverte, utilisée par
/// l'assistant d'évolution (J3I § 7, D8) et l'UI Workbench (Phase 6).
/// </summary>
public class ContinuiteTemporelleTests
{
    private static (string, PeriodeReglementaire) P(string cle, string eff, string? fin = null)
        => (cle, PeriodeReglementaire.Creer(eff, fin));

    [Fact]
    public void Valider_accepte_periode_unique_ouverte()
    {
        var r = ContinuiteTemporelle.Valider(new[] { P("DOC", "2008-01-01") });
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public void Valider_accepte_chronologie_continue_fermee()
    {
        var r = ContinuiteTemporelle.Valider(new[]
        {
            P("DOC", "2008-01-01", "2010-12-31"),
            P("DOC", "2011-01-01", "2014-12-31"),
            P("DOC", "2015-01-01", "2024-12-31"),
        });
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public void Valider_accepte_derniere_periode_ouverte_apres_fermees()
    {
        var r = ContinuiteTemporelle.Valider(new[]
        {
            P("DOC", "2008-01-01", "2010-12-31"),
            P("DOC", "2011-01-01", null),
        });
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public void Valider_rejette_chevauchement()
    {
        var r = ContinuiteTemporelle.Valider(new[]
        {
            P("DOC", "2008-01-01", "2010-12-31"),
            P("DOC", "2010-06-01", "2012-12-31"),   // chevauche
        });
        Assert.True(r.IsFailure);
        Assert.Equal("validation", r.Error.Code);
        Assert.Contains("chevauchement", r.Error.Message);
    }

    [Fact]
    public void Valider_rejette_trou()
    {
        var r = ContinuiteTemporelle.Valider(new[]
        {
            P("DOC", "2008-01-01", "2010-12-31"),
            P("DOC", "2011-06-01", "2012-12-31"),   // trou entre 31/12/2010 et 01/06/2011
        });
        Assert.True(r.IsFailure);
        Assert.Contains("trou", r.Error.Message);
    }

    [Fact]
    public void Valider_rejette_deux_periodes_ouvertes_pour_meme_cle()
    {
        var r = ContinuiteTemporelle.Valider(new[]
        {
            P("DOC", "2008-01-01", null),
            P("DOC", "2020-01-01", null),
        });
        Assert.True(r.IsFailure);
        Assert.Contains("ouvertes", r.Error.Message);
    }

    [Fact]
    public void Valider_isole_les_cles_independantes()
    {
        // Deux clés différentes : un trou sur l'une n'affecte pas l'autre.
        var r = ContinuiteTemporelle.Valider(new[]
        {
            P("DOC", "2008-01-01", "2010-12-31"),
            P("DOC", "2015-01-01", "2018-12-31"),   // trou sur DOC
            P("QUALIF", "2008-01-01", "2024-12-31"),  // QUALIF continu
        });
        Assert.True(r.IsFailure);
        Assert.Contains("DOC", r.Error.Message);
        Assert.DoesNotContain("QUALIF", r.Error.Message);
    }

    [Fact]
    public void Lendemain_passe_a_l_annee_suivante()
    {
        Assert.Equal("2025-01-01", ContinuiteTemporelle.Lendemain("2024-12-31"));
        Assert.Equal("2024-03-01", ContinuiteTemporelle.Lendemain("2024-02-29"));   // bissextile
        Assert.Equal("2025-01-01", ContinuiteTemporelle.Lendemain("2024-12-31"));
    }
}
