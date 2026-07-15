namespace PaieEducation.Domain.Workbench.Internal;

/// <summary>
/// Erreur locale au Domain. Copie minimale de <c>PaieEducation.Shared.Results.Error</c>
/// : voir <see cref="Guard"/> pour la justification. Pas de référence à Shared.
/// </summary>
public sealed record Error(string Code, string Message)
{
    public static Error Validation(string message) => new("validation", message);
    public static Error NotFound(string message) => new("not_found", message);
    public static Error Conflict(string message) => new("conflict", message);
    public static Error Failure(string message) => new("failure", message);
}

