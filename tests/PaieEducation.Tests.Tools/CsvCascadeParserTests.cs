using PaieEducation.Seeding;
using PaieEducation.Seeding.Models;

namespace PaieEducation.Tests.Tools;

/// <summary>
/// Tests unitaires du <see cref="CsvCascadeParser"/>. Le parser est pur
/// (aucune dépendance disque/DB), donc les tests utilisent un
/// <see cref="StringReader"/> avec un échantillon de CSV en mémoire.
/// </summary>
public class CsvCascadeParserTests
{
    private static async Task<IReadOnlyList<PaieEducation.Seeding.Models.CascadeRow>> Parse(string csv)
    {
        var parser = new CsvCascadeParser();
        using var sr = new StringReader(csv);
        return await parser.ParseAsync(sr);
    }

    [Fact]
    public async Task Parse_header_multiligne_avec_les_4_indices_est_saute()
    {
        // Le header contient des retours chariot dans les champs entre guillemets.
        // Le parser doit ignorer ces fragments et commencer par la première
        // ligne qui ressemble à une ligne de données (commence par un chiffre).
        const string csv = """
            Num_Ord;Type_Contrat;Type_Filiere;Type_Secteur;Type_Personnel;Corps_Filiere;Grades;Categorie;"Indice Minimum avant
            01/03/2022";"Indice Minimum
            A partir du 01/03/2022";"Indice Minimum
            A partir du 01/01/2023";"Indice Minimum
            A partir du 01/01/2024"
            1;Statut_Fonctionnaire;ADMIN;Education Nationale;Personnels d'Education;Corps des Adjoints de l'Education;Adjoint de l'Education;7;348;398;473;548
            2;Statut_Fonctionnaire;ADMIN;Education Nationale;Personnels d'Education;Corps des Adjoints de l'Education;Adjoint principal de l'Education;8;379;429;504;579
            """;

        var rows = await Parse(csv);

        Assert.Equal(2, rows.Count);
        Assert.Equal(1, rows[0].NumOrd);
        Assert.Equal(7, rows[0].Categorie);
        Assert.Equal(348, rows[0].IndiceAv2022_03);
        Assert.Equal(548, rows[0].IndiceAp2024_01);
    }

    [Fact]
    public async Task Parse_les_accents_et_apostrophes_sont_preserves()
    {
        // Le CSV est en CP1252 ; en UTF-8 mémoire, les accents sont déjà OK.
        const string csv = """
            Num_Ord;Type_Contrat;Type_Filiere;Type_Secteur;Type_Personnel;Corps_Filiere;Grades;Categorie;"A";"B";"C";"D"
            1;Statut_Fonctionnaire;ENSEIGNANT;Education Nationale;Personnels Enseignants;Corps des Professeurs d'Education;Professeur d'éducation;12;537;587;662;737
            """;

        var rows = await Parse(csv);

        Assert.Single(rows);
        // Le champ Corps_Filiere est le libellé complet (avec préfixe "Corps des...").
        Assert.Equal("Corps des Professeurs d'Education", rows[0].CorpsFiliere);
        // Le champ Grade contient l'apostrophe droite (typographie française).
        Assert.Equal("Professeur d'éducation", rows[0].Grade);
    }

    [Fact]
    public async Task Parse_les_espaces_multiples_dans_un_champ_sont_normalises()
    {
        const string csv = """
            Num_Ord;Type_Contrat;Type_Filiere;Type_Secteur;Type_Personnel;Corps_Filiere;Grades;Categorie;"A";"B";"C";"D"
            1;Statut_Fonctionnaire;ADMIN;Education Nationale;Personnels d'Education;Corps des Adjoints de l'Education;Adjoint  de  l'Education;7;348;398;473;548
            """;

        var rows = await Parse(csv);

        Assert.Single(rows);
        Assert.Equal("Adjoint de l'Education", rows[0].Grade);
    }

    [Fact]
    public async Task Parse_une_ligne_avec_mauvais_nombre_de_colonnes_est_ignoree()
    {
        const string csv = """
            Num_Ord;Type_Contrat;Type_Filiere;Type_Secteur;Type_Personnel;Corps_Filiere;Grades;Categorie;"A";"B";"C";"D"
            1;Statut_Fonctionnaire;ADMIN;Education Nationale;Personnels d'Education;Corps des Adjoints de l'Education;Adjoint;7
            2;Statut_Fonctionnaire;ADMIN;Education Nationale;Personnels d'Education;Corps des Adjoints de l'Education;Adjoint;8;379;429;504;579
            """;

        var rows = await Parse(csv);

        Assert.Single(rows);
        Assert.Equal(2, rows[0].NumOrd);
    }

    [Fact]
    public async Task Parse_un_champ_donnee_avec_retour_chariot_littteral_entre_guillemets_est_reconstruit()
    {
        // Régression n° 134 (Cascade_Corps_Grades_30526.csv) : le libellé de
        // grade est entre guillemets et contient un retour chariot littéral
        // ("Inspecteur de l'orientation et de la\nguidance scolaire et
        // professionnelle"). Avant correctif, la lecture ligne à ligne
        // scindait l'enregistrement en 2 fragments de mauvaise arité, tous
        // deux silencieusement ignorés — la ligne disparaissait du seed.
        const string csv = "Num_Ord;Type_Contrat;Type_Filiere;Type_Secteur;Type_Personnel;Corps_Filiere;Grades;Categorie;\"A\";\"B\";\"C\";\"D\"\n"
            + "134;Statut_Fonctionnaire;INSPECTION;Education Nationale;Personnels d'Inspection;"
            + "Corps des Inspecteurs de l'orientation et de la guidance scolaire et professionnelle;"
            + "\"Inspecteur de l'orientation et de la\nguidance scolaire et professionnelle\";15;666;716;791;866\n";

        var rows = await Parse(csv);

        var row = Assert.Single(rows);
        Assert.Equal(134, row.NumOrd);
        Assert.Equal("Inspecteur de l'orientation et de la guidance scolaire et professionnelle", row.Grade);
        Assert.Equal(15, row.Categorie);
        Assert.Equal(866, row.IndiceAp2024_01);
    }

    [Fact]
    public async Task Parse_un_champ_avec_point_virgule_entre_guillemets_nest_pas_scinde()
    {
        const string csv = """
            Num_Ord;Type_Contrat;Type_Filiere;Type_Secteur;Type_Personnel;Corps_Filiere;Grades;Categorie;"A";"B";"C";"D"
            1;Statut_Fonctionnaire;ADMIN;Education Nationale;Personnels d'Education;Corps des Adjoints de l'Education;"Adjoint; principal";7;348;398;473;548
            """;

        var rows = await Parse(csv);

        var row = Assert.Single(rows);
        Assert.Equal("Adjoint; principal", row.Grade);
    }

    [Fact]
    public async Task Parse_un_csv_vide_retourne_une_liste_vide()
    {
        var rows = await Parse("");
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Parse_uniquement_un_header_retourne_une_liste_vide()
    {
        const string csv = """
            Num_Ord;Type_Contrat;Type_Filiere;Type_Secteur;Type_Personnel;Corps_Filiere;Grades;Categorie;"A";"B";"C";"D"
            """;
        var rows = await Parse(csv);
        Assert.Empty(rows);
    }
}
