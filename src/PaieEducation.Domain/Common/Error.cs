namespace PaieEducation.Domain.Common;

/// <summary>
/// Erreur du Domain de calcul. Même patron que <c>Workbench.Internal.Error</c>
/// et <c>Shared.Results.Error</c> : le Domain ne peut pas dépendre de Shared
/// (cf. <c>DependencyRulesTests.Domain_ne_depend_d_aucun_projet</c>), donc le
/// noyau <c>Result/Error/Guard</c> est redéfini localement. Ce noyau
/// <c>Common</c> est partagé par tout le sous-arbre de calcul (Phase 4).
/// </summary>
public sealed record Error(string Code, string Message)
{
    public static Error Validation(string message) => new("validation", message);
    public static Error NotFound(string message) => new("not_found", message);
    public static Error Conflict(string message) => new("conflict", message);
    public static Error Failure(string message) => new("failure", message);

    /// <summary>Erreur de syntaxe d'une formule (lexer/parser).</summary>
    public static Error Syntaxe(string message) => new("syntaxe", message);

    /// <summary>Erreur d'évaluation (variable inconnue, division par zéro, ...).</summary>
    public static Error Evaluation(string message) => new("evaluation", message);

    /// <summary>Cycle détecté dans le graphe de dépendances des rubriques.</summary>
    public static Error Cycle(string message) => new("cycle", message);
}
