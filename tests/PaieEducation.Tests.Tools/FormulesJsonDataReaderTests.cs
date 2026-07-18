using PaieEducation.Seeding;

namespace PaieEducation.Tests.Tools;

/// <summary>
/// Tests Lot 1.3 final — lecteur du jeu de données formules embarqué
/// (<c>formules_v1.json</c>). Couvre :
///   - chargement et structure (1 rubrique TRAITEMENT + 6 formules)
///   - déterminisme du hash sur données inchangées
///   - cohérence du hash avec celui de BaremeIrgDataReader (même contrat)
/// </summary>
public class FormulesJsonDataReaderTests
{
    [Fact]
    public void Load_retourne_1_rubrique_TRAITEMENT_et_6_formules()
    {
        var data = FormulesJsonDataReader.Load();

        Assert.Equal("1.0", data.Version);
        Assert.Equal("2008-01-01", data.DateEffet);
        Assert.Equal("TRAITEMENT", data.RubriqueTraitement.Id);
        Assert.Equal("Traitement mensuel de base", data.RubriqueTraitement.Libelle);
        Assert.Equal(6, data.Formules.Count);
    }

    [Fact]
    public void Load_liste_toutes_les_formules_attendues_avec_leurs_expressions()
    {
        var data = FormulesJsonDataReader.Load();
        var ids = data.Formules.Select(f => f.RubriqueId).ToHashSet();

        Assert.Contains("TRAITEMENT", ids);
        Assert.Contains("EXP_PEDAG", ids);
        Assert.Contains("PAPP", ids);
        Assert.Contains("QUALIF", ids);
        Assert.Contains("DOC_PEDAG", ids);
        Assert.Contains("ISSRP_45", ids);

        var t = data.Formules.Single(f => f.RubriqueId == "TRAITEMENT");
        Assert.Equal("(INDICE_MIN + INDICE_ECH) * VPI", t.Expression);
    }

    [Fact]
    public void HashLigne_est_deterministe_pour_les_memes_donnees()
    {
        var formule = new FormuleSeed("X-1", "TBASE * 0.04", "test");
        var h1 = FormulesJsonDataReader.HashLigne(formule);
        var h2 = FormulesJsonDataReader.HashLigne(formule);

        Assert.Equal(h1, h2);
        Assert.StartsWith("sha256:", h1);
    }

    [Fact]
    public void HashLigne_derive_quand_une_donnee_change()
    {
        var f1 = new FormuleSeed("X-1", "TBASE * 0.04", "test");
        var f2 = f1 with { Expression = "TBASE * 0.05" };

        Assert.NotEqual(FormulesJsonDataReader.HashLigne(f1), FormulesJsonDataReader.HashLigne(f2));
    }
}
