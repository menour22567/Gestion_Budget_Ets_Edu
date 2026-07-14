namespace PaieEducation.Tools.Seeding.Models;

/// <summary>
/// Une ligne brute du CSV <c>Cascade_Corps_Grades_*.csv</c>.
/// Le CSV source utilise le séparateur <c>;</c>, l'encodage CP1252 (Windows-1252)
/// et un en-tête multi-ligne (les libellés des colonnes Indice contiennent des
/// retours chariot). Toutes les chaînes sont **déjà normalisées** par
/// <see cref="CsvCascadeParser"/> (accents préservés, espaces nettoyés).
/// </summary>
/// <remarks>
/// Les indices sont stockés comme <c>int</c> : la source ne contient que des
/// valeurs entières (348, 398, 473, 548 pour la catégorie 7).
/// </remarks>
public sealed record CascadeRow(
    int NumOrd,
    string TypeContrat,
    string TypeFiliere,
    string TypeSecteur,
    string TypePersonnel,
    string CorpsFiliere,
    string Grade,
    int Categorie,
    int IndiceAv2022_03,
    int IndiceAp2022_03,
    int IndiceAp2023_01,
    int IndiceAp2024_01);
