using System.Globalization;
using System.Text;
using PaieEducation.Domain.Common;

namespace PaieEducation.Domain.Calcul.Formules;

/// <summary>
/// Analyseur d'expressions de formule (lexer + descente récursive). Produit un
/// <see cref="FormulaNode"/> à partir du texte lu en base, ou un échec de
/// syntaxe. Grammaire (priorités usuelles) :
/// <code>
///   expression := terme   (('+' | '-') terme)*
///   terme      := facteur (('*' | '/') facteur)*
///   facteur    := ('-' facteur) | primaire
///   primaire   := NOMBRE | IDENT | IDENT '(' args? ')' | '(' expression ')'
///   args       := expression (',' expression)*
/// </code>
/// </summary>
public sealed class FormulaParser
{
    private enum Kind { Number, Ident, Plus, Minus, Star, Slash, LParen, RParen, Comma, End }

    private readonly record struct Token(Kind Kind, string Text, decimal Number, int Position);

    private readonly IReadOnlyList<Token> _tokens;
    private int _pos;
    private Error? _erreur;

    private FormulaParser(IReadOnlyList<Token> tokens) => _tokens = tokens;

    /// <summary>Analyse une expression. Renvoie l'arbre ou un échec de syntaxe.</summary>
    public static Result<FormulaNode> Parser(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return Result.Failure<FormulaNode>(Error.Syntaxe("Formule vide."));

        var lex = Tokeniser(expression);
        if (lex.IsFailure)
            return Result.Failure<FormulaNode>(lex.Error);

        var parser = new FormulaParser(lex.Value);
        var node = parser.ParseExpression();
        if (parser._erreur is not null)
            return Result.Failure<FormulaNode>(parser._erreur);
        if (parser.Courant().Kind != Kind.End)
            return Result.Failure<FormulaNode>(
                Error.Syntaxe($"Symbole inattendu « {parser.Courant().Text} » à la position {parser.Courant().Position}."));

        return Result.Success(node!);
    }

    // ---------------------------------------------------------------- Lexer ----

    private static Result<IReadOnlyList<Token>> Tokeniser(string s)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            switch (c)
            {
                case '+': tokens.Add(new Token(Kind.Plus, "+", 0, i)); i++; continue;
                case '-': tokens.Add(new Token(Kind.Minus, "-", 0, i)); i++; continue;
                case '*': tokens.Add(new Token(Kind.Star, "*", 0, i)); i++; continue;
                case '/': tokens.Add(new Token(Kind.Slash, "/", 0, i)); i++; continue;
                case '(': tokens.Add(new Token(Kind.LParen, "(", 0, i)); i++; continue;
                case ')': tokens.Add(new Token(Kind.RParen, ")", 0, i)); i++; continue;
                case ',': tokens.Add(new Token(Kind.Comma, ",", 0, i)); i++; continue;
            }

            if (char.IsDigit(c) || c == '.')
            {
                var start = i;
                var sb = new StringBuilder();
                var pointVu = false;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.'))
                {
                    if (s[i] == '.')
                    {
                        if (pointVu)
                            return Result.Failure<IReadOnlyList<Token>>(
                                Error.Syntaxe($"Nombre mal formé à la position {start}."));
                        pointVu = true;
                    }
                    sb.Append(s[i]);
                    i++;
                }
                if (!decimal.TryParse(sb.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var val))
                    return Result.Failure<IReadOnlyList<Token>>(
                        Error.Syntaxe($"Nombre invalide « {sb} » à la position {start}."));
                tokens.Add(new Token(Kind.Number, sb.ToString(), val, start));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                var sb = new StringBuilder();
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_'))
                {
                    sb.Append(s[i]);
                    i++;
                }
                tokens.Add(new Token(Kind.Ident, sb.ToString(), 0, start));
                continue;
            }

            return Result.Failure<IReadOnlyList<Token>>(
                Error.Syntaxe($"Caractère non reconnu « {c} » à la position {i}."));
        }

        tokens.Add(new Token(Kind.End, "", 0, s.Length));
        return Result.Success<IReadOnlyList<Token>>(tokens);
    }

    // --------------------------------------------------------------- Parser ----

    private Token Courant() => _tokens[_pos];

    private FormulaNode? ParseExpression()
    {
        var left = ParseTerme();
        while (_erreur is null && (Courant().Kind == Kind.Plus || Courant().Kind == Kind.Minus))
        {
            var op = Courant().Kind == Kind.Plus ? '+' : '-';
            _pos++;
            var right = ParseTerme();
            if (_erreur is not null) return null;
            left = new BinaryNode(op, left!, right!);
        }
        return left;
    }

    private FormulaNode? ParseTerme()
    {
        var left = ParseFacteur();
        while (_erreur is null && (Courant().Kind == Kind.Star || Courant().Kind == Kind.Slash))
        {
            var op = Courant().Kind == Kind.Star ? '*' : '/';
            _pos++;
            var right = ParseFacteur();
            if (_erreur is not null) return null;
            left = new BinaryNode(op, left!, right!);
        }
        return left;
    }

    private FormulaNode? ParseFacteur()
    {
        if (Courant().Kind == Kind.Minus)
        {
            _pos++;
            var operand = ParseFacteur();
            if (_erreur is not null) return null;
            return new UnaryNode('-', operand!);
        }
        if (Courant().Kind == Kind.Plus)
        {
            _pos++;
            return ParseFacteur();
        }
        return ParsePrimaire();
    }

    private FormulaNode? ParsePrimaire()
    {
        var tok = Courant();
        switch (tok.Kind)
        {
            case Kind.Number:
                _pos++;
                return new NumberNode(tok.Number);

            case Kind.LParen:
            {
                _pos++;
                var inner = ParseExpression();
                if (_erreur is not null) return null;
                if (Courant().Kind != Kind.RParen)
                {
                    _erreur = Error.Syntaxe($"Parenthèse fermante attendue à la position {Courant().Position}.");
                    return null;
                }
                _pos++;
                return inner;
            }

            case Kind.Ident:
            {
                _pos++;
                if (Courant().Kind == Kind.LParen)
                    return ParseAppel(tok.Text);
                return new IdentifierNode(tok.Text);
            }

            default:
                _erreur = Error.Syntaxe($"Expression attendue à la position {tok.Position} (trouvé « {tok.Text} »).");
                return null;
        }
    }

    private FormulaNode? ParseAppel(string nom)
    {
        _pos++; // consomme '('
        var args = new List<FormulaNode>();
        if (Courant().Kind != Kind.RParen)
        {
            while (true)
            {
                var arg = ParseExpression();
                if (_erreur is not null) return null;
                args.Add(arg!);
                if (Courant().Kind == Kind.Comma) { _pos++; continue; }
                break;
            }
        }
        if (Courant().Kind != Kind.RParen)
        {
            _erreur = Error.Syntaxe($"Parenthèse fermante d'appel attendue à la position {Courant().Position}.");
            return null;
        }
        _pos++; // consomme ')'
        return new CallNode(nom, args);
    }
}
