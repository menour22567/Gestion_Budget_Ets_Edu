using PaieEducation.Domain.Calcul.ValueObjects;

namespace PaieEducation.Domain.Calcul.Irg;

/// <summary>Profil fiscal du salarié (pilote le lissage spécial IRG).</summary>
public enum ProfilFiscal
{
    /// <summary>Salarié standard.</summary>
    Standard,

    /// <summary>
    /// Travailleur handicapé moteur/mental/non-voyant/sourd-muet ou retraité du
    /// régime général — éligible au lissage spécial (JO 33/2020, non cumulable
    /// avec le lissage général).
    /// </summary>
    HandicapeOuRetraiteRG
}

/// <summary>
/// Tranche du barème IRG mensuel. La borne supérieure délimite la bande : une
/// tranche taxe la portion du revenu comprise entre la borne supérieure de la
/// tranche précédente et la sienne. <see cref="BorneSup"/> nulle = tranche
/// sommitale (+infini).
/// </summary>
public sealed record IrgTranche(decimal BorneInf, decimal? BorneSup, decimal Taux);

/// <summary>
/// Règle de période IRG résolue (<c>IRGReglesPeriode</c> V006/V007 + barème
/// associé). Les coefficients/constantes de lissage sont des fractions exactes
/// (V007). Objet d'entrée pur — la sélection de la règle à la date de paie et le
/// chargement depuis la base sont la responsabilité du pipeline.
/// </summary>
public sealed record IrgReglePeriode(
    string Code,
    decimal ExonerationSeuil,
    decimal AbattementTaux,
    decimal AbattementMin,
    decimal AbattementMax,
    Fraction CoefGeneral,
    Fraction ConstGeneral,
    Fraction CoefSpecial,
    Fraction ConstSpecial,
    decimal PlafondSpecial,
    IReadOnlyList<IrgTranche> Tranches);

/// <summary>
/// Résultat détaillé du calcul IRG — chaque étape est exposée pour
/// l'explicabilité (ExplainabilityEngine, critère d'acceptation Phase 4).
/// </summary>
public sealed record IrgResultat(
    decimal RevenuImposable,
    decimal Brut,
    decimal Abattement,
    decimal ApresAbattement,
    decimal Final,
    string EtapeAppliquee);
