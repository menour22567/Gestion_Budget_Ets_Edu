namespace PaieEducation.Domain.Workbench.Constants;

/// <summary>
/// Constantes pour le type de valeur d'un barème (<c>RubriqueBaremes.TypeValeur</c>).
/// Même rôle que <see cref="BaremeDimensionKeys"/> : évite les chaînes
/// magiques "TAUX"/"MONTANT" dupliquées entre lecture et écriture.
/// </summary>
public static class BaremeTypeValeurKeys
{
    public const string Taux = "TAUX";
    public const string Montant = "MONTANT";

    /// <summary>Les valeurs valides pour la colonne <c>RubriqueBaremes.TypeValeur</c> (CHECK V008).</summary>
    public static readonly IReadOnlyList<string> Valides = [Taux, Montant];
}
