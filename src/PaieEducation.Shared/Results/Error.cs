namespace PaieEducation.Shared.Results;

/// <summary>
/// Erreur normalisée (code technique + message lisible) utilisée par <see cref="Result"/>.
/// Voir docs/CONVENTIONS.md — les cas métier attendus passent par Result, pas par exceptions.
/// </summary>
public sealed record Error(string Code, string Message)
{
    /// <summary>Absence d'erreur (résultat réussi).</summary>
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>Donnée invalide (validation applicative ou de présentation).</summary>
    public static Error Validation(string message) => new("validation", message);

    /// <summary>Entité introuvable.</summary>
    public static Error NotFound(string message) => new("not_found", message);

    /// <summary>Conflit avec une règle métier ou un invariant.</summary>
    public static Error Conflict(string message) => new("conflict", message);

    /// <summary>Échec technique ou inattendu.</summary>
    public static Error Failure(string message) => new("failure", message);

    /// <summary>Erreur de syntaxe d'une formule (lexer/parser).</summary>
    public static Error Syntaxe(string message) => new("syntaxe", message);

    /// <summary>Erreur d'évaluation (variable inconnue, division par zéro, ...).</summary>
    public static Error Evaluation(string message) => new("evaluation", message);

    /// <summary>Cycle détecté dans le graphe de dépendances des rubriques.</summary>
    public static Error Cycle(string message) => new("cycle", message);

    /// <inheritdoc />
    public override string ToString() => $"[{Code}] {Message}";
}
