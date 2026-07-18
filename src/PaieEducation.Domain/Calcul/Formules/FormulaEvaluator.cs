using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Domain.Calcul.Formules;

/// <summary>
/// Évalue un <see cref="FormulaNode"/> contre un <see cref="IFormulaContext"/>.
/// Pur : aucune I/O, aucune horloge. Toute résolution de variable/barème/source
/// passe par le contexte. Les erreurs (variable inconnue, division par zéro,
/// fonction inconnue, mauvais arité) remontent en <see cref="Result{T}"/>.
/// </summary>
public sealed class FormulaEvaluator
{
    /// <summary>Évalue l'arbre. Renvoie la valeur ou le premier échec rencontré.</summary>
    public Result<decimal> Evaluer(FormulaNode node, IFormulaContext contexte)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(contexte);
        return Eval(node, contexte);
    }

    private static Result<decimal> Eval(FormulaNode node, IFormulaContext ctx) => node switch
    {
        NumberNode n => Result.Success(n.Value),
        IdentifierNode id => ctx.ResoudreVariable(id.Name),
        UnaryNode u => EvalUnaire(u, ctx),
        BinaryNode b => EvalBinaire(b, ctx),
        CallNode c => EvalAppel(c, ctx),
        _ => Result.Failure<decimal>(Error.Evaluation($"Nœud de formule non supporté : {node.GetType().Name}."))
    };

    private static Result<decimal> EvalUnaire(UnaryNode u, IFormulaContext ctx)
    {
        var operand = Eval(u.Operand, ctx);
        if (operand.IsFailure) return operand;
        return Result.Success(u.Operator == '-' ? -operand.Value : operand.Value);
    }

    private static Result<decimal> EvalBinaire(BinaryNode b, IFormulaContext ctx)
    {
        var left = Eval(b.Left, ctx);
        if (left.IsFailure) return left;
        var right = Eval(b.Right, ctx);
        if (right.IsFailure) return right;

        return b.Operator switch
        {
            '+' => Result.Success(left.Value + right.Value),
            '-' => Result.Success(left.Value - right.Value),
            '*' => Result.Success(left.Value * right.Value),
            '/' => right.Value == 0m
                ? Result.Failure<decimal>(Error.Evaluation("Division par zéro dans une formule."))
                : Result.Success(left.Value / right.Value),
            _ => Result.Failure<decimal>(Error.Evaluation($"Opérateur inconnu « {b.Operator} »."))
        };
    }

    private static Result<decimal> EvalAppel(CallNode c, IFormulaContext ctx)
    {
        switch (c.Name.ToLowerInvariant())
        {
            case "round":
                return EvalRound(c, ctx);
            case "abs":
                return EvalUnaireFonction(c, ctx, Math.Abs);
            case "min":
                return EvalReduction(c, ctx, Math.Min, "min");
            case "max":
                return EvalReduction(c, ctx, Math.Max, "max");
            case "bareme":
                return EvalBareme(c, ctx);
            case "valeursource":
                return EvalSource(c, ctx);
            default:
                return Result.Failure<decimal>(Error.Evaluation($"Fonction inconnue « {c.Name} »."));
        }
    }

    private static Result<decimal> EvalRound(CallNode c, IFormulaContext ctx)
    {
        if (c.Args.Count is < 1 or > 2)
            return Result.Failure<decimal>(Error.Evaluation("round(x[, n]) attend 1 ou 2 arguments."));

        var x = Eval(c.Args[0], ctx);
        if (x.IsFailure) return x;

        var decimales = 0;
        if (c.Args.Count == 2)
        {
            var n = Eval(c.Args[1], ctx);
            if (n.IsFailure) return n;
            if (n.Value < 0 || n.Value > 28 || n.Value != Math.Truncate(n.Value))
                return Result.Failure<decimal>(Error.Evaluation($"round : nombre de décimales invalide ({n.Value})."));
            decimales = (int)n.Value;
        }
        return Result.Success(Math.Round(x.Value, decimales, MidpointRounding.AwayFromZero));
    }

    private static Result<decimal> EvalUnaireFonction(CallNode c, IFormulaContext ctx, Func<decimal, decimal> f)
    {
        if (c.Args.Count != 1)
            return Result.Failure<decimal>(Error.Evaluation($"{c.Name}(x) attend 1 argument."));
        var x = Eval(c.Args[0], ctx);
        return x.IsFailure ? x : Result.Success(f(x.Value));
    }

    private static Result<decimal> EvalReduction(
        CallNode c, IFormulaContext ctx, Func<decimal, decimal, decimal> f, string nom)
    {
        if (c.Args.Count < 1)
            return Result.Failure<decimal>(Error.Evaluation($"{nom}(...) attend au moins 1 argument."));

        var premier = Eval(c.Args[0], ctx);
        if (premier.IsFailure) return premier;
        var acc = premier.Value;
        for (var i = 1; i < c.Args.Count; i++)
        {
            var v = Eval(c.Args[i], ctx);
            if (v.IsFailure) return v;
            acc = f(acc, v.Value);
        }
        return Result.Success(acc);
    }

    // bareme(RUB, DIM) et valeurSource(RUB) : les arguments sont des identifiants
    // bruts (codes), pas des variables à résoudre. On lit le nom du nœud.

    private static Result<decimal> EvalBareme(CallNode c, IFormulaContext ctx)
    {
        if (c.Args.Count != 2 || c.Args[0] is not IdentifierNode rub || c.Args[1] is not IdentifierNode dim)
            return Result.Failure<decimal>(Error.Evaluation("bareme(RUB, DIM) attend deux identifiants (rubrique, dimension)."));
        return ctx.ResoudreBareme(rub.Name, dim.Name);
    }

    private static Result<decimal> EvalSource(CallNode c, IFormulaContext ctx)
    {
        if (c.Args.Count != 1 || c.Args[0] is not IdentifierNode rub)
            return Result.Failure<decimal>(Error.Evaluation("valeurSource(RUB) attend un identifiant (rubrique)."));
        return ctx.ResoudreSource(rub.Name);
    }

    /// <summary>
    /// Raccourci parse + évalue. Utile pour les tests et les usages one-shot ;
    /// le pipeline préfère parser une fois puis évaluer N fois.
    /// </summary>
    public Result<decimal> Evaluer(string expression, IFormulaContext contexte)
    {
        var arbre = FormulaParser.Parser(expression);
        return arbre.IsFailure ? Result.Failure<decimal>(arbre.Error) : Evaluer(arbre.Value, contexte);
    }
}
