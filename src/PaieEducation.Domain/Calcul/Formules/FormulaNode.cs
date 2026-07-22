namespace PaieEducation.Domain.Calcul.Formules;

/// <summary>
/// Nœud de l'arbre syntaxique d'une formule. Une formule réglementaire est
/// <b>lue en base</b> (<c>RubriqueFormules.Expression</c>) et jamais codée en
/// dur (ADR-0005, critère d'acceptation Phase 4 « 0 règle dans le code »).
/// </summary>
public abstract record FormulaNode;

/// <summary>Littéral numérique (ex. <c>0.04</c>, <c>2500</c>).</summary>
public sealed record NumberNode(decimal Value) : FormulaNode;

/// <summary>
/// Référence à une variable résolue par le contexte (ex. <c>TBASE</c>, <c>TRT</c>,
/// <c>ECH</c>, <c>CAT</c>, ou un paramètre versionné comme <c>TAUX_IEP</c>).
/// </summary>
public sealed record IdentifierNode(string Name) : FormulaNode;

/// <summary>Opération unaire (négation).</summary>
public sealed record UnaryNode(char Operator, FormulaNode Operand) : FormulaNode;

/// <summary>Opération binaire (<c>+ - * /</c>).</summary>
public sealed record BinaryNode(char Operator, FormulaNode Left, FormulaNode Right) : FormulaNode;

/// <summary>
/// Appel de fonction. Fonctions supportées : <c>round(x, n)</c>, <c>min(...)</c>,
/// <c>max(...)</c>, <c>abs(x)</c>, <c>bareme(RUB, DIM)</c>, <c>valeurSource(RUB)</c>.
/// Pour <c>bareme</c> et <c>valeurSource</c>, les arguments sont des identifiants
/// bruts (codes rubrique/dimension), pas des variables à résoudre.
/// </summary>
public sealed record CallNode(string Name, IReadOnlyList<FormulaNode> Args) : FormulaNode;

/// <summary>
/// Utilitaires de parcours de l'AST. Introduits avec P10 (FormulaEditor avancé)
/// pour le feedback live (compte de nœuds) et l'auto-complétion (liste des
/// identificateurs référencés). Pas de logique métier ici — pur helpers.
/// </summary>
public static class FormulaNodeWalker
{
    /// <summary>Compte le nombre total de nœuds de l'arbre (racine incluse).</summary>
    public static int Compter(FormulaNode racine) => racine switch
    {
        null => 0,
        NumberNode => 1,
        IdentifierNode => 1,
        UnaryNode u => 1 + Compter(u.Operand),
        BinaryNode b => 1 + Compter(b.Left) + Compter(b.Right),
        CallNode c => 1 + c.Args.Sum(Compter),
        _ => 1
    };

    /// <summary>Collecte les noms des identificateurs (variables) référencés, dans l'ordre de lecture.</summary>
    public static IReadOnlyList<string> CollecterIdentificateurs(FormulaNode racine)
    {
        var result = new List<string>();
        Visiter(racine, n => { if (n is IdentifierNode id) result.Add(id.Name); });
        return result;
    }

    /// <summary>Collecte les noms des fonctions appelées.</summary>
    public static IReadOnlyList<string> CollecterAppels(FormulaNode racine)
    {
        var result = new List<string>();
        Visiter(racine, n => { if (n is CallNode c) result.Add(c.Name.ToLowerInvariant()); });
        return result;
    }

    private static void Visiter(FormulaNode? racine, Action<FormulaNode> action)
    {
        if (racine is null) return;
        action(racine);
        switch (racine)
        {
            case UnaryNode u: Visiter(u.Operand, action); break;
            case BinaryNode b: Visiter(b.Left, action); Visiter(b.Right, action); break;
            case CallNode c: foreach (var a in c.Args) Visiter(a, action); break;
        }
    }
}
