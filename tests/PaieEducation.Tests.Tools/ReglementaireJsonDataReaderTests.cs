using PaieEducation.Seeding;

namespace PaieEducation.Tests.Tools;

/// <summary>
/// Tests Lot 1.3 final — lecteur du référentiel réglementaire embarqué
/// (<c>referentiel_reglementaire_v1.json</c>). Couvre :
///   - structure des 4 sections externalisées (rubriques, barèmes, cotisations, paramètres)
///   - préservation des nullables (periodiciteVersement, borneSup, taux facultatif)
///   - déterminisme du hash, détection de drift
/// </summary>
public class ReglementaireJsonDataReaderTests
{
    [Fact]
    public void Load_retourne_4_sections_avec_les_volumes_attendus()
    {
        var data = ReglementaireJsonDataReader.Load();

        Assert.Equal("1.0", data.Version);
        Assert.Equal(10, data.Rubriques.Count);
        Assert.Equal(5, data.Baremes.Count);
        Assert.Equal(3, data.Cotisations.Count);
        Assert.Equal(10, data.Parametres.Count);
    }

    [Fact]
    public void Load_liste_les_10_rubriques_attendues()
    {
        var data = ReglementaireJsonDataReader.Load();
        var ids = data.Rubriques.Select(r => r.Id).ToHashSet();

        Assert.Contains("IEP_FONC", ids);
        Assert.Contains("IEP_CONT", ids);
        Assert.Contains("EXP_PEDAG", ids);
        Assert.Contains("PAPP", ids);
        Assert.Contains("QUALIF", ids);
        Assert.Contains("DOC_PEDAG", ids);
        Assert.Contains("ISSRP_45", ids);
        Assert.Contains("ISSRP_30", ids);
        Assert.Contains("ISSRP_15", ids);
        Assert.Contains("IRG", ids);
    }

    [Fact]
    public void Load_preserve_le_versement_TRIMESTRIELLE_de_PAPP()
    {
        // PAPP a une périodicité MENSUELLE mais un versement TRIMESTRIELLE :
        // nuance réglementaire que le code dur C# portait. On vérifie que
        // la migration JSON n'a rien perdu.
        var data = ReglementaireJsonDataReader.Load();
        var papp = data.Rubriques.Single(r => r.Id == "PAPP");

        Assert.Equal("MENSUELLE", papp.Periodicite);
        Assert.Equal("TRIMESTRIELLE", papp.PeriodiciteVersement);
        Assert.True(papp.EstAffectableManuellement);
    }

    [Fact]
    public void Load_preserve_les_flags_Impot_pour_IRG()
    {
        // IRG est un IMPÔT — ni imposable ni cotisable par lui-même.
        var data = ReglementaireJsonDataReader.Load();
        var irg = data.Rubriques.Single(r => r.Id == "IRG");

        Assert.Equal("IMPOT", irg.Nature);
        Assert.False(irg.EstImposable);
        Assert.False(irg.EstCotisable);
        Assert.False(irg.EstAffectableManuellement);
    }

    [Fact]
    public void Load_preserve_les_bornes_ouvertes_pour_QUALIF_et_DOC_PEDAG()
    {
        // QUALIF-CAT-GE13 et DOC_PEDAG-CAT-GE13 ont BorneSup = null (+infini).
        var data = ReglementaireJsonDataReader.Load();
        var qualifGe13 = data.Baremes.Single(b => b.Id == "RB-QUALIF-CAT-GE13");
        var docGe13 = data.Baremes.Single(b => b.Id == "RB-DOCPEDAG-CAT-GE13");

        Assert.Equal("13", qualifGe13.BorneInf);
        Assert.Null(qualifGe13.BorneSup);
        Assert.Equal("0.45", qualifGe13.Valeur);
        Assert.Equal("TAUX", qualifGe13.TypeValeur);

        Assert.Equal("13", docGe13.BorneInf);
        Assert.Null(docGe13.BorneSup);
        Assert.Equal("3000", docGe13.Valeur);
        Assert.Equal("MONTANT", docGe13.TypeValeur);
    }

    [Fact]
    public void Load_preserve_le_taux_null_des_cotisations_facultatives()
    {
        // MUTUELLE et OEUVRES_SOCIALES sont à MONTANT_FIXE (taux null en base).
        // Le JSON porte "taux": null — le converter doit désérialiser en double?.
        var data = ReglementaireJsonDataReader.Load();
        var mutuelle = data.Cotisations.Single(c => c.Code == "MUTUELLE");
        var oeuvres = data.Cotisations.Single(c => c.Code == "OEUVRES_SOCIALES");

        Assert.Null(mutuelle.Taux);
        Assert.Equal("MONTANT_FIXE", mutuelle.AssietteRef);
        Assert.Null(oeuvres.Taux);
    }

    [Fact]
    public void Load_preserve_le_taux_de_la_SS_a_9_pct()
    {
        // Q3b : Sécurité sociale part ouvrière 9 %.
        var data = ReglementaireJsonDataReader.Load();
        var ss = data.Cotisations.Single(c => c.Code == "SS");

        Assert.Equal(0.09, ss.Taux);
        Assert.Equal("ASSIETTE_COTISABLE", ss.AssietteRef);
        Assert.Equal("OBLIGATOIRE_SALARIALE", ss.Type);
    }

    [Fact]
    public void Load_liste_les_10_parametres_attendus_avec_leurs_cles()
    {
        var data = ReglementaireJsonDataReader.Load();
        var cles = data.Parametres.Select(p => p.Cle).ToHashSet();

        Assert.Contains("ARRONDI_MODE", cles);
        Assert.Contains("ARRONDI_PRECISION", cles);
        Assert.Contains("VALEUR_POINT_DEFAUT", cles);
        Assert.Contains("BASE_PAPP", cles);
        Assert.Contains("NOTE_MAX_PAPP", cles);
        Assert.Contains("PLAFOND_LISSAGE_GENERAL", cles);
        Assert.Contains("SEUIL_EXONERATION_IRG", cles);
        Assert.Contains("IEP_TAUX_PUBLIC_PCT", cles);
        Assert.Contains("IEP_TAUX_PRIVE_PCT", cles);
        Assert.Contains("IEP_PLAFOND_PCT", cles);
    }

    [Fact]
    public void HashLigne_est_deterministe_pour_les_memes_donnees()
    {
        var cot = new CotisationReglementaireSeed(
            "X-1", "X", "Test", "OBLIGATOIRE_SALARIALE", 0.05, "ASSIETTE_COTISABLE",
            true, "2024-01-01", "test");
        var h1 = ReglementaireJsonDataReader.HashLigne(cot);
        var h2 = ReglementaireJsonDataReader.HashLigne(cot);

        Assert.Equal(h1, h2);
        Assert.StartsWith("sha256:", h1);
    }

    [Fact]
    public void HashLigne_derive_quand_une_donnee_change()
    {
        var c1 = new CotisationReglementaireSeed(
            "X-1", "X", "Test", "OBLIGATOIRE_SALARIALE", 0.05, "ASSIETTE_COTISABLE",
            true, "2024-01-01", "test");
        var c2 = c1 with { Taux = 0.06 };

        Assert.NotEqual(
            ReglementaireJsonDataReader.HashLigne(c1),
            ReglementaireJsonDataReader.HashLigne(c2));
    }
}
