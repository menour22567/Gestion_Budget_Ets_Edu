using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.Internal;

namespace PaieEducation.Domain.Workbench.ValueObjects;

/// <summary>
/// Barème indexé — une <c>(rubrique, dimension, clé)</c> avec une valeur (TAUX ou
/// MONTANT) et une période de validité. V008 § 8bis.1 — table
/// <c>RubriqueBaremes</c> ; l'index unique <c>IX_RubriqueBaremes_Rub_Dim_Borne_Date</c>
/// garantit l'unicité par (rubrique, dimension, clé de tranche, date d'effet).
/// </summary>
/// <remarks>
/// Le FormulaEngine expose <c>bareme(RUBRIQUE, dimension)</c> qui résout la bonne
/// ligne à la date de paie. La clé de tranche (<c>BorneInf</c>) est textuelle
/// pour porter des dimensions discrètes (catégories, types d'établissement, codes
/// de corps) ; <c>BorneSup</c> = null signifie « +infini ».
/// </remarks>
public sealed record BaremeValue
{
    /// <summary>Rubrique concernée (FK vers <c>Rubriques.Id</c>).</summary>
    public string RubriqueId { get; }

    /// <summary>Dimension d'indexation.</summary>
    public BaremeDimension Dimension { get; }

    /// <summary>Clé de tranche textuelle (ex. <c>"7"</c> pour catégorie, <c>"PRIMAIRE"</c> pour type d'établissement).</summary>
    public string BorneInf { get; }

    /// <summary>Borne supérieure textuelle, ou <c>null</c> pour « +infini ».</summary>
    public string? BorneSup { get; }

    /// <summary>Type de la valeur (TAUX = fraction, MONTANT = DA).</summary>
    public BaremeTypeValeur TypeValeur { get; }

    /// <summary>Valeur textuelle canonique (parsée par le moteur).</summary>
    public string Valeur { get; }

    /// <summary>Période de validité.</summary>
    public PeriodeReglementaire Periode { get; }

    private BaremeValue(
        string rubriqueId,
        BaremeDimension dimension,
        string borneInf,
        string? borneSup,
        BaremeTypeValeur typeValeur,
        string valeur,
        PeriodeReglementaire periode)
    {
        RubriqueId = rubriqueId;
        Dimension = dimension;
        BorneInf = borneInf;
        BorneSup = borneSup;
        TypeValeur = typeValeur;
        Valeur = valeur;
        Periode = periode;
    }

    /// <summary>Fabrique validante.</summary>
    public static BaremeValue Creer(
        string rubriqueId,
        BaremeDimension dimension,
        string borneInf,
        string? borneSup,
        BaremeTypeValeur typeValeur,
        string valeur,
        PeriodeReglementaire periode)
    {
        Guard.AgainstNullOrWhiteSpace(rubriqueId);
        Guard.AgainstNullOrWhiteSpace(borneInf);
        Guard.AgainstNullOrWhiteSpace(valeur);
        return new BaremeValue(rubriqueId, dimension, borneInf, borneSup, typeValeur, valeur, periode);
    }

    /// <summary>
    /// Vrai si la clé de tranche couvre la valeur de clé demandée. Pour les
    /// dimensions discrètes (catégorie, type d'établissement, corps) BorneInf =
    /// BorneSup ; pour les tranches continues (ancienneté, échelon), BorneSup
    /// peut être non-null.
    /// </summary>
    /// <remarks>
    /// Comparaison « naturelle » : si les deux opérandes sont des entiers
    /// (catégorie, ancienneté, échelon), on compare numériquement pour éviter
    /// le piège lexicographique (<c>"8" &gt; "12"</c>). Sinon, comparaison
    /// ordinale (chaînes comme <c>"PRIMAIRE"</c>, <c>"LYCEE"</c>).
    /// </remarks>
    public bool Couvre(string cleDemandee)
    {
        if (int.TryParse(cleDemandee, out var cleInt)
            && int.TryParse(BorneInf, out var infInt))
        {
            if (BorneSup is null)
            {
                return cleInt >= infInt;
            }
            if (int.TryParse(BorneSup, out var supInt))
            {
                return cleInt >= infInt && cleInt <= supInt;
            }
            // BorneSup non numérique et BorneInf numérique : fallback lexical
        }
        // Fallback : comparaison lexicale (codes non-numériques : PRIMAIRE, CEM, etc.)
        var dansBorneInfStr = string.CompareOrdinal(cleDemandee, BorneInf) >= 0;
        var dansBorneSupStr = BorneSup is null
            || string.CompareOrdinal(cleDemandee, BorneSup) <= 0;
        return dansBorneInfStr && dansBorneSupStr;
    }
}

