using System.Globalization;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Domain.Calcul.ValueObjects;

/// <summary>
/// Fraction rationnelle exacte, stockée réduite (dénominateur &gt; 0). Portée par
/// les coefficients et constantes de lissage IRG (V007) : « 8/3 », « 20000/3 »,
/// « 137/51 », « 27925/8 », « 93/61 », « 81213/41 ». Un entier (« 30000 ») est
/// une fraction de dénominateur 1.
/// </summary>
/// <remarks>
/// Le stockage TEXT en base garde la valeur réglementaire exacte (V007 : un
/// <c>double</c> ne représente pas « 8/3 »). Les calculs de montant sont
/// ensuite arrondis au dinar par le service d'arrondi centralisé : appliquer la
/// fraction au numérateur avant la division (<see cref="Multiplier"/>) préserve
/// la précision jusqu'à l'arrondi final.
/// </remarks>
public readonly record struct Fraction
{
    public long Numerateur { get; }
    public long Denominateur { get; }

    private Fraction(long numerateur, long denominateur)
    {
        Numerateur = numerateur;
        Denominateur = denominateur;
    }

    /// <summary>Fraction nulle (0/1).</summary>
    public static Fraction Zero => new(0, 1);

    /// <summary>Fraction unité (1/1) — coefficient neutre (pas de transformation).</summary>
    public static Fraction Un => new(1, 1);

    /// <summary>
    /// Construit une fraction réduite. <paramref name="denominateur"/> non nul ;
    /// le signe est porté par le numérateur.
    /// </summary>
    public static Fraction Creer(long numerateur, long denominateur)
    {
        if (denominateur == 0)
            throw new ArgumentException("Le dénominateur d'une fraction ne peut pas être nul.", nameof(denominateur));

        if (denominateur < 0)
        {
            numerateur = -numerateur;
            denominateur = -denominateur;
        }

        var pgcd = Pgcd(Math.Abs(numerateur), denominateur);
        return new Fraction(numerateur / pgcd, denominateur / pgcd);
    }

    /// <summary>
    /// Parse une fraction canonique « a/b » ou un entier « a ». Renvoie un
    /// <see cref="Result{T}"/> d'échec (plutôt qu'une exception) sur une entrée
    /// malformée : la valeur vient de la base, elle peut être erronée.
    /// </summary>
    public static Result<Fraction> Parser(string? texte)
    {
        if (string.IsNullOrWhiteSpace(texte))
            return Result.Failure<Fraction>(Error.Validation("Fraction vide."));

        var t = texte.Trim();
        var slash = t.IndexOf('/');
        if (slash < 0)
        {
            return long.TryParse(t, NumberStyles.Integer | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out var entier)
                ? Result.Success(Creer(entier, 1))
                : Result.Failure<Fraction>(Error.Validation($"Entier invalide : « {texte} »."));
        }

        var numTexte = t[..slash].Trim();
        var denTexte = t[(slash + 1)..].Trim();
        if (!long.TryParse(numTexte, NumberStyles.Integer | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var num)
            || !long.TryParse(denTexte, NumberStyles.Integer | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var den))
        {
            return Result.Failure<Fraction>(Error.Validation($"Fraction invalide : « {texte} »."));
        }

        if (den == 0)
            return Result.Failure<Fraction>(Error.Validation($"Dénominateur nul : « {texte} »."));

        return Result.Success(Creer(num, den));
    }

    /// <summary>Valeur décimale de la fraction.</summary>
    public decimal VersDecimal() => (decimal)Numerateur / Denominateur;

    /// <summary>
    /// Applique la fraction à une valeur : <c>x × Num / Den</c>. La multiplication
    /// précède la division pour préserver la précision.
    /// </summary>
    public decimal Multiplier(decimal x) => x * Numerateur / Denominateur;

    public override string ToString() =>
        Denominateur == 1
            ? Numerateur.ToString(CultureInfo.InvariantCulture)
            : $"{Numerateur.ToString(CultureInfo.InvariantCulture)}/{Denominateur.ToString(CultureInfo.InvariantCulture)}";

    private static long Pgcd(long a, long b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }
        return a == 0 ? 1 : a;
    }
}
