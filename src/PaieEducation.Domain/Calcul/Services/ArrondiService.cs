using PaieEducation.Shared.Money;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Domain.Calcul.Services;

/// <summary>Mode d'arrondi des montants (paramètre <c>ARRONDI_MODE</c>, Q9b).</summary>
public enum ModeArrondi
{
    /// <summary>Au dinar le plus proche (défaut réglementaire, Q9b).</summary>
    DinarPlusProche,

    /// <summary>À la dizaine de dinars la plus proche.</summary>
    Dizaine,

    /// <summary>Au centime (2 décimales).</summary>
    Centime
}

/// <summary>
/// Service d'arrondi centralisé (RM-120, Q9). Un seul point d'arrondi pour tout
/// le moteur : aucune formule n'arrondit elle-même son résultat intermédiaire —
/// c'est le pipeline qui applique cet arrondi sur les montants finaux.
/// </summary>
/// <remarks>
/// Le mode par défaut vient du paramètre <c>ARRONDI_MODE</c> (base, Q9b :
/// « au dinar le plus proche »). L'arrondi commercial (<see cref="MidpointRounding.AwayFromZero"/>)
/// est le comportement attendu d'une paie : 0,5 DA monte à 1 DA.
/// </remarks>
public sealed class ArrondiService
{
    private readonly ModeArrondi _mode;

    public ArrondiService(ModeArrondi mode = ModeArrondi.DinarPlusProche) => _mode = mode;

    /// <summary>Mode actif du service.</summary>
    public ModeArrondi Mode => _mode;

    /// <summary>
    /// Parse le mode depuis la valeur textuelle du paramètre <c>ARRONDI_MODE</c>.
    /// Échec explicite sur une valeur inconnue (plutôt qu'un défaut silencieux).
    /// </summary>
    public static Result<ModeArrondi> ParserMode(string? valeur) => valeur?.Trim().ToUpperInvariant() switch
    {
        "DINAR_PLUS_PROCHE" => Result.Success(ModeArrondi.DinarPlusProche),
        "DIZAINE" => Result.Success(ModeArrondi.Dizaine),
        "CENTIME" => Result.Success(ModeArrondi.Centime),
        _ => Result.Failure<ModeArrondi>(Error.Validation($"Mode d'arrondi inconnu : « {valeur} »."))
    };

    /// <summary>Arrondit un montant selon le mode du service.</summary>
    public decimal Arrondir(decimal montant) => _mode switch
    {
        ModeArrondi.DinarPlusProche => Math.Round(montant, 0, MidpointRounding.AwayFromZero),
        ModeArrondi.Dizaine => Math.Round(montant / 10m, 0, MidpointRounding.AwayFromZero) * 10m,
        ModeArrondi.Centime => Math.Round(montant, 2, MidpointRounding.AwayFromZero),
        _ => montant
    };

    public Money Arrondir(Money montant) => new(Arrondir(montant.Amount), montant.Currency);

    /// <summary>
    /// Arrondit à un nombre de décimales arbitraire — point d'entrée dédié à la fonction
    /// de formule <c>round(x[, n])</c> (<see cref="Formules.FormulaEvaluator"/>), distinct
    /// de <see cref="Arrondir(decimal)"/> qui applique le <see cref="ModeArrondi"/> paramétré
    /// à un montant final de bulletin. Reste dans <c>ArrondiService.cs</c> : seul point
    /// d'arrondi centralisé autorisé par ADR-0011 (garde d'architecture
    /// <c>DependencyRulesTests.Arrondi_centralise_uniquement_dans_ArrondiService</c>).
    /// </summary>
    public static decimal ArrondirDecimales(decimal valeur, int decimales) =>
        Math.Round(valeur, decimales, MidpointRounding.AwayFromZero);
}
