using System.Runtime.CompilerServices;

namespace PaieEducation.Domain.Workbench.Internal;

/// <summary>
/// Clauses de garde locales au Domain. Copie minimale de
/// <c>PaieEducation.Shared.Guards.Guard</c> : le Domain ne peut pas dépendre
/// de Shared (cf. <c>DependencyRulesTests.Domain_ne_depend_d_aucun_projet</c>).
/// Une duplication ciblée et documentée vaut mieux qu'une dépendance qui
/// casse l'invariant d'architecture (ADR-0001).
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

