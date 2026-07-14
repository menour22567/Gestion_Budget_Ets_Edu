using System.Runtime.CompilerServices;

namespace PaieEducation.Shared.Guards;

/// <summary>
/// Clauses de garde pour valider les préconditions et protéger les invariants.
/// </summary>
public static class Guard
{
    /// <summary>Vérifie que la référence n'est pas nulle.</summary>
    public static T AgainstNull<T>(
        T? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : class
        => value ?? throw new ArgumentNullException(paramName);

    /// <summary>Vérifie que la chaîne n'est ni nulle ni composée uniquement d'espaces.</summary>
    public static string AgainstNullOrWhiteSpace(
        string? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("La valeur ne peut pas être vide.", paramName)
            : value;

    /// <summary>Vérifie que le nombre n'est pas strictement négatif.</summary>
    public static int AgainstNegative(
        int value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        => value < 0
            ? throw new ArgumentOutOfRangeException(paramName, value, "La valeur ne peut pas être négative.")
            : value;
}
