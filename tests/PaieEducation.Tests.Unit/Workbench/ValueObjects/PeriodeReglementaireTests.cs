using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Tests.Unit.Workbench.ValueObjects;

/// <summary>
/// Tests du Value Object <see cref="PeriodeReglementaire"/>. Couvre la
/// création (invariants) et la résolution par date (V003-V007 patron de
/// résolution unique — J3E § 1).
/// </summary>
public class PeriodeReglementaireTests
{
    [Fact]
    public void Creer_accepte_periode_ouverte()
    {
        var p = PeriodeReglementaire.Creer("2025-01-01", dateFin: null);
        Assert.Equal("2025-01-01", p.DateEffet);
        Assert.Null(p.DateFin);
    }

    [Fact]
    public void Creer_accepte_periode_fermee()
    {
        var p = PeriodeReglementaire.Creer("2025-01-01", "2025-12-31");
        Assert.Equal("2025-12-31", p.DateFin);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Creer_rejette_DateEffet_vide(string? dateEffet)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            PeriodeReglementaire.Creer(dateEffet!, null));
    }

    [Fact]
    public void Creer_rejette_DateFin_anterieure_a_DateEffet()
    {
        Assert.Throws<ArgumentException>(() =>
            PeriodeReglementaire.Creer("2025-12-31", "2025-01-01"));
    }

    [Fact]
    public void Contient_renvoie_vrai_pour_date_dans_la_periode()
    {
        var p = PeriodeReglementaire.Creer("2025-01-01", "2025-12-31");
        Assert.True(p.Contient("2025-06-15"));
        Assert.True(p.Contient("2025-01-01"));   // bornes inclusives
        Assert.True(p.Contient("2025-12-31"));
    }

    [Fact]
    public void Contient_renvoie_faux_hors_periode()
    {
        var p = PeriodeReglementaire.Creer("2025-01-01", "2025-12-31");
        Assert.False(p.Contient("2024-12-31"));
        Assert.False(p.Contient("2026-01-01"));
    }

    [Fact]
    public void Contient_renvoie_vrai_pour_toute_date_quand_ouverte()
    {
        var p = PeriodeReglementaire.Creer("2025-01-01", null);
        Assert.True(p.Contient("2025-06-15"));
        Assert.True(p.Contient("2099-12-31"));   // borne sup = +infini
    }

    [Fact]
    public void Chevauche_detecte_chevauchement_simple()
    {
        var a = PeriodeReglementaire.Creer("2025-01-01", "2025-06-30");
        var b = PeriodeReglementaire.Creer("2025-06-15", "2025-12-31");
        Assert.True(a.Chevauche(b));
        Assert.True(b.Chevauche(a));   // symétrie
    }

    [Fact]
    public void Chevauche_renvoie_faux_quand_jouxtees()
    {
        // [01..30/06] et [01/07..31/12] sont jouxtées (pas de trou, pas de chevauchement)
        var a = PeriodeReglementaire.Creer("2025-01-01", "2025-06-30");
        var b = PeriodeReglementaire.Creer("2025-07-01", "2025-12-31");
        Assert.False(a.Chevauche(b));
    }

    [Fact]
    public void Chevauche_ouverte_englobe_une_periode_fermee()
    {
        var ouverte = PeriodeReglementaire.Creer("2025-01-01", null);
        var fermee = PeriodeReglementaire.Creer("2025-06-01", "2025-12-31");
        Assert.True(ouverte.Chevauche(fermee));
    }
}
