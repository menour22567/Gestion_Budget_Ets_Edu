using PaieEducation.Seeding;

namespace PaieEducation.Tests.Tools;

/// <summary>
/// Tests chantier P2 (audit du 19/07/2026) — lecteur des groupes DNF ISSRP
/// et des 4 grades hors catégorie embarqués
/// (<c>groupes_dnf_issrp_v1.json</c>). Couvre : structure, résolution des
/// références de listes de grades (union ordonnée), déterminisme du hash.
/// </summary>
public class GroupesDnfIssrpJsonDataReaderTests
{
    [Fact]
    public void Load_retourne_les_4_listes_de_grades_nommees_avec_les_volumes_attendus()
    {
        var data = GroupesDnfIssrpJsonDataReader.Load();

        Assert.Equal("1.0", data.Version);
        Assert.Equal(50, data.Grades["issrp45Direct"].Count);
        Assert.Equal(7, data.Grades["issrpOrigine"].Count);
        Assert.Equal(20, data.Grades["issrp30Direct"].Count);
        Assert.Equal(15, data.Grades["issrp15Direct"].Count);
    }

    [Fact]
    public void Load_retourne_les_6_groupes_DNF()
    {
        var data = GroupesDnfIssrpJsonDataReader.Load();

        Assert.Equal(6, data.Groupes.Count);
        var ids = data.Groupes.Select(g => g.GroupeId).ToHashSet();
        Assert.Contains("GE-ISSRP45-DIRECT", ids);
        Assert.Contains("GE-ISSRP45-ORIGINE", ids);
        Assert.Contains("GE-ISSRP30-DIRECT", ids);
        Assert.Contains("GE-ISSRP30-ORIGINE", ids);
        Assert.Contains("GE-ISSRP15-DIRECT", ids);
        Assert.Contains("GE-ISSRP15-HIST", ids);
    }

    [Fact]
    public void Load_preserve_les_conditions_conditionnelles_par_origine()
    {
        var data = GroupesDnfIssrpJsonDataReader.Load();

        var origine45 = data.Groupes.Single(g => g.GroupeId == "GE-ISSRP45-ORIGINE");
        Assert.Equal(2, origine45.Conditions.Count);
        Assert.Contains(origine45.Conditions, c => c.CritereId == "ORIGINE_STATUTAIRE" && c.Valeur == "ENSEIGNANT");

        var origine30 = data.Groupes.Single(g => g.GroupeId == "GE-ISSRP30-ORIGINE");
        Assert.Contains(origine30.Conditions, c => c.CritereId == "ORIGINE_STATUTAIRE" && c.Valeur == "AUTRE");
    }

    [Fact]
    public void ResoudreGrades_direct_renvoie_la_liste_nommee_dans_l_ordre()
    {
        var data = GroupesDnfIssrpJsonDataReader.Load();

        var resolus = GroupesDnfIssrpJsonDataReader.ResoudreGrades(data, ["issrp45Direct"]);

        Assert.Equal(50, resolus.Count);
        Assert.Equal("CDL-G014", resolus[0]); // premier élément, ordre préservé
        Assert.Equal("IDLS-G148", resolus[^1]); // dernier élément (grade hors catégorie réintégré)
    }

    [Fact]
    public void ResoudreGrades_historique_unionne_les_4_listes_dans_l_ordre_sans_deduplication()
    {
        var data = GroupesDnfIssrpJsonDataReader.Load();
        var histGroupe = data.Groupes.Single(g => g.GroupeId == "GE-ISSRP15-HIST");
        var condition = Assert.Single(histGroupe.Conditions);

        var resolus = GroupesDnfIssrpJsonDataReader.ResoudreGrades(data, condition.GradesRefs!);

        Assert.Equal(50 + 7 + 20 + 15, resolus.Count); // 92, aucune déduplication (union C# d'origine)
        Assert.Equal("CDL-G014", resolus[0]); // début de issrp45Direct
        Assert.Equal("SDL-G007", resolus[50]); // début de issrpOrigine, juste après les 50 premiers
        Assert.Equal("ADL-G001", resolus[57]); // début de issrp30Direct (50+7)
        Assert.Equal("ADSE-G035", resolus[77]); // début de issrp15Direct (50+7+20)
        Assert.Equal("IDLS-G147", resolus[^1]); // dernier élément de issrp15Direct
    }

    [Fact]
    public void ResoudreGrades_reference_inconnue_leve_explicitement()
    {
        var data = GroupesDnfIssrpJsonDataReader.Load();

        Assert.Throws<InvalidOperationException>(() =>
            GroupesDnfIssrpJsonDataReader.ResoudreGrades(data, ["liste-qui-n-existe-pas"]));
    }

    [Fact]
    public void Load_preserve_les_4_grades_hors_categorie_et_leur_grille()
    {
        var data = GroupesDnfIssrpJsonDataReader.Load();
        var hc = data.GradesHorsCategorie;

        Assert.Equal("INSPECTION", hc.Filiere.Id);
        Assert.Equal("IDLS", hc.Corps.Id);
        Assert.Equal(2, hc.Categories.Count);
        Assert.Equal(18, hc.Categories.Single(c => c.Id == "HC-S1").Niveau);
        Assert.Equal(19, hc.Categories.Single(c => c.Id == "HC-S2").Niveau);

        Assert.Equal(6, hc.GrilleIndiciaire.Count);
        Assert.Equal(980, hc.GrilleIndiciaire.Single(g => g.CategorieId == "HC-S1" && g.DateEffet == "2022-03-01").Indice);
        Assert.Equal(1190, hc.GrilleIndiciaire.Single(g => g.CategorieId == "HC-S2" && g.DateEffet == "2024-01-01").Indice);
        Assert.Null(hc.GrilleIndiciaire.Single(g => g.CategorieId == "HC-S2" && g.DateEffet == "2024-01-01").DateFin);

        Assert.Equal(4, hc.Grades.Count);
        var ids = hc.Grades.Select(g => g.Id).ToHashSet();
        Assert.Contains("IDLS-G144", ids);
        Assert.Contains("IDLS-G145", ids);
        Assert.Contains("IDLS-G146", ids);
        Assert.Contains("IDLS-G148", ids);
    }

    [Fact]
    public void HashLigne_reutilise_le_meme_mecanisme_deterministe_que_les_autres_sections()
    {
        var data = GroupesDnfIssrpJsonDataReader.Load();
        var groupe = data.Groupes[0];

        var h1 = ReglementaireJsonDataReader.HashLigne(groupe);
        var h2 = ReglementaireJsonDataReader.HashLigne(groupe);

        Assert.Equal(h1, h2);
        Assert.StartsWith("sha256:", h1);
    }
}
