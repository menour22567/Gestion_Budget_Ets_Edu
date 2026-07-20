using PaieEducation.Domain.Workbench.Enums;

namespace PaieEducation.Domain.Workbench.Constants;

/// <summary>
/// Constantes et parser pour les dimensions de barème (<c>RubriqueBaremes.Dimension</c>).
/// Unifie les 3 parsers dupliqués dans <c>PayrollReadRepository</c>,
/// <c>WorkbenchReadRepository</c> et <c>CalculationPipeline</c>.
/// </summary>
public static class BaremeDimensionKeys
{
    public const string Categorie = "CATEGORIE";
    public const string Echelon = "ECHELON";
    public const string Anciennete = "ANCIENNETE";
    public const string TypeEtablissement = "TYPE_ETABLISSEMENT";
    public const string Corps = "CORPS";
    public const string Grade = "GRADE";

    /// <summary>
    /// Les valeurs valides pour la colonne <c>RubriqueBaremes.Dimension</c>
    /// (CHECK V008) — <see cref="Grade"/> en est délibérément absent : il est
    /// valide pour <see cref="BaremeDimension"/> (utilisé ailleurs, ex.
    /// <c>ReglesEligibilite.CritereId</c>) mais rejeté par la contrainte CHECK
    /// de cette table précise si on tente de l'y insérer.
    /// </summary>
    public static readonly IReadOnlyList<string> ValidesPourRubriqueBaremes =
        [Categorie, Echelon, Anciennete, TypeEtablissement, Corps];

    /// <summary>
    /// Parse une chaîne de caractères en <see cref="BaremeDimension"/>.
    /// </summary>
    public static BaremeDimension Parser(string dimension) => dimension switch
    {
        Categorie => BaremeDimension.Categorie,
        Echelon => BaremeDimension.Echelon,
        Anciennete => BaremeDimension.Anciennete,
        TypeEtablissement => BaremeDimension.TypeEtablissement,
        Corps => BaremeDimension.Corps,
        Grade => BaremeDimension.Grade,
        _ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "Dimension de barème inconnue.")
    };

    /// <summary>
    /// Normalise une dimension en sa représentation canonique (première lettre majuscule).
    /// Utilisé par <c>CalculationPipeline.NormaliserDimension</c>.
    /// </summary>
    public static string Normaliser(string dimension) => dimension switch
    {
        Categorie => "Categorie",
        Echelon => "Echelon",
        Anciennete => "Anciennete",
        TypeEtablissement => "TypeEtablissement",
        Corps => "Corps",
        Grade => "Grade",
        _ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "Dimension de barème inconnue.")
    };
}
