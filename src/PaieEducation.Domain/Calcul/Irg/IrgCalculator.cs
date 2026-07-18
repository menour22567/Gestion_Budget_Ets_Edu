using PaieEducation.Domain.Calcul.Constants;
using PaieEducation.Domain.Calcul.ValueObjects;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Domain.Calcul.Irg;

/// <summary>
/// Calcul de l'IRG mensuel — implémentation fidèle du pseudo-code de référence
/// (<c>Reglementation/IRG_Algerie_2008_2026/CALCUL IRG ALGERIE.txt</c>, arbitrage
/// INC-09) : barème progressif mensuel → abattement 40 % borné [1 000 ; 1 500] →
/// exonération ≤ seuil → lissage spécial (handicapé/retraité RG) ou général.
/// </summary>
/// <remarks>
/// Point clé (levée de l'ambiguïté ⛔Q4b) : les lissages s'appliquent à l'IRG
/// <b>après abattement</b>, PAS au revenu — <c>IRG = irgApresAbattement × coef −
/// const</c> (JO 33/2020, Art. 104). Les coefficients/constantes sont des
/// fractions exactes (V007). Pur, déterministe : aucune I/O, aucune horloge.
/// <br/>
/// C8.1 — seuil d'exonération et plafond de lissage général lus depuis
/// <c>Parametres</c> (SEUIL_EXONERATION_IRG, PLAFOND_LISSAGE_GENERAL), plus
/// hardcodés.
/// </remarks>
public sealed class IrgCalculator
{
    private readonly decimal _seuilExoneration;
    private readonly decimal _plafondLissageGeneral;

    public IrgCalculator(decimal seuilExoneration, decimal plafondLissageGeneral)
    {
        _seuilExoneration = seuilExoneration;
        _plafondLissageGeneral = plafondLissageGeneral;
    }

    /// <summary>
    /// Calcule l'IRG dû pour un revenu mensuel imposable, un profil fiscal et une
    /// règle de période résolue.
    /// </summary>
    public Result<IrgResultat> Calculer(decimal revenuImposable, ProfilFiscal profil, IrgReglePeriode regle)
    {
        ArgumentNullException.ThrowIfNull(regle);
        if (revenuImposable < 0)
            return Result.Failure<IrgResultat>(Error.Validation("Revenu imposable négatif."));
        if (regle.Tranches.Count == 0)
            return Result.Failure<IrgResultat>(Error.Validation($"Barème IRG vide pour la période « {regle.Code} »."));

        // 2. IRG brut progressif sur le barème mensuel.
        var brut = CalculerBrut(revenuImposable, regle.Tranches);

        // 3. Abattement standard 40 %, borné [min ; max].
        var abattement = Math.Clamp(brut * regle.AbattementTaux, regle.AbattementMin, regle.AbattementMax);
        var apresAbattement = Math.Max(0m, brut - abattement);

        // 4. Exonération : si activée (seuil > 0) et revenu ≤ seuil → IRG = 0.
        if (regle.ExonerationSeuil > 0 && revenuImposable <= _seuilExoneration)
            return Ok(revenuImposable, brut, abattement, 0m, IrgEtapes.Exoneration);

        // 5. Lissage spécial (handicapé/retraité RG), non cumulable avec le général.
        if (profil == ProfilFiscal.HandicapeOuRetraiteRG
            && EstActif(regle.CoefSpecial, regle.ConstSpecial)
            && revenuImposable > _seuilExoneration
            && revenuImposable < regle.PlafondSpecial)
        {
            var lisse = regle.CoefSpecial.Multiplier(apresAbattement) - regle.ConstSpecial.VersDecimal();
            return Ok(revenuImposable, brut, abattement, Math.Max(0m, lisse), IrgEtapes.LissageSpecial);
        }

        // 6. Lissage général (seuil < revenu < plafond).
        if (EstActif(regle.CoefGeneral, regle.ConstGeneral)
            && revenuImposable > _seuilExoneration
            && revenuImposable < _plafondLissageGeneral)
        {
            var lisse = regle.CoefGeneral.Multiplier(apresAbattement) - regle.ConstGeneral.VersDecimal();
            return Ok(revenuImposable, brut, abattement, Math.Max(0m, lisse), IrgEtapes.LissageGeneral);
        }

        // 7. Sinon : IRG après abattement (planché à 0).
        return Ok(revenuImposable, brut, abattement, apresAbattement, IrgEtapes.Standard);
    }

    /// <summary>
    /// IRG brut progressif sur un barème mensuel. Chaque tranche taxe la portion
    /// du revenu comprise entre la borne supérieure de la tranche précédente et
    /// la sienne. Exposé publiquement : c'est le cas de référence verrouillé par
    /// test (54 800 → 8 596 sur barème 2022, 11 440 sur barème 2008).
    /// </summary>
    public static decimal CalculerBrut(decimal revenu, IReadOnlyList<IrgTranche> tranches)
    {
        var ordonnees = tranches.OrderBy(t => t.BorneInf).ToList();
        var seuilBas = 0m;
        var brut = 0m;
        foreach (var t in ordonnees)
        {
            var borneHaute = t.BorneSup ?? revenu;
            var effectiveHaute = Math.Min(revenu, borneHaute);
            var portion = effectiveHaute - seuilBas;
            if (portion > 0)
                brut += portion * t.Taux;
            seuilBas = borneHaute;
            if (revenu <= borneHaute)
                break;
        }
        return brut;
    }

    /// <summary>
    /// Un lissage est « actif » pour une période si son coefficient n'est pas
    /// l'identité (coef = 1 ET const = 0 ⇒ pas de transformation, ex. période
    /// « avant 2020-06 »).
    /// </summary>
    private static bool EstActif(Fraction coef, Fraction cst)
        => !(coef == Fraction.Un && cst == Fraction.Zero);

    private static Result<IrgResultat> Ok(
        decimal revenu, decimal brut, decimal abattement, decimal final, string etape)
        => Result.Success(new IrgResultat(revenu, brut, abattement, Math.Max(0m, brut - abattement), final, etape));
}
