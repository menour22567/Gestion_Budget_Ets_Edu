using System.Runtime.CompilerServices;

namespace PaieEducation.Domain.Common;

/// <summary>
/// Clauses de garde du Domain de calcul. Même justification que
/// <c>Workbench.Internal.Guard</c> : le Domain ne peut pas dépendre de Shared.
/// </summary>
public static class Guard
{
    public static T AgainstNull<T>(T? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : class
        => value ?? throw new ArgumentNullException(paramName);

    public static string AgainstNullOrWhiteSpace(
        string? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("La valeur ne peut pas être vide.", paramName)
            : value;
}
