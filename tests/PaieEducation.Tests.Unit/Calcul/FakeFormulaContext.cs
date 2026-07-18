using PaieEducation.Domain.Calcul.Formules;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Tests.Unit.Calcul;

/// <summary>
/// Contexte de formule en mémoire pour les tests de l'évaluateur. Résout les
/// variables / barèmes / sources depuis des dictionnaires ; échoue proprement
/// sur une clé absente (comme le fera le pipeline réel).
/// </summary>
internal sealed class FakeFormulaContext : IFormulaContext
{
    private readonly Dictionary<string, decimal> _variables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, decimal> _baremes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, decimal> _sources = new(StringComparer.Ordinal);

    public FakeFormulaContext Var(string nom, decimal valeur) { _variables[nom] = valeur; return this; }
    public FakeFormulaContext Bareme(string rub, string dim, decimal valeur) { _baremes[$"{rub}|{dim}"] = valeur; return this; }
    public FakeFormulaContext Source(string rub, decimal valeur) { _sources[rub] = valeur; return this; }

    public Result<decimal> ResoudreVariable(string nom) =>
        _variables.TryGetValue(nom, out var v)
            ? Result.Success(v)
            : Result.Failure<decimal>(Error.Evaluation($"Variable inconnue : {nom}"));

    public Result<decimal> ResoudreBareme(string rubrique, string dimension) =>
        _baremes.TryGetValue($"{rubrique}|{dimension}", out var v)
            ? Result.Success(v)
            : Result.Failure<decimal>(Error.NotFound($"Barème absent : {rubrique}/{dimension}"));

    public Result<decimal> ResoudreSource(string rubrique) =>
        _sources.TryGetValue(rubrique, out var v)
            ? Result.Success(v)
            : Result.Failure<decimal>(Error.NotFound($"Source absente : {rubrique}"));
}
