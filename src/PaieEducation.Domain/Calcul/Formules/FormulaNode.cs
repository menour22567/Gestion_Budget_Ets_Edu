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
