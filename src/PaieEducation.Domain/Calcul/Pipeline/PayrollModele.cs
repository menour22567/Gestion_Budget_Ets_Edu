using PaieEducation.Domain.Calcul.Audit;
using PaieEducation.Domain.Calcul.Cotisations;
using PaieEducation.Domain.Calcul.Explicabilite;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Shared.Money;

namespace PaieEducation.Domain.Calcul.Pipeline;

/// <summary>Nature d'une rubrique (V004 <c>Rubriques.Nature</c>).</summary>
public enum NatureRubrique
{
    Gain,
    Retenue,
    Cotisation,
    Impot
}

/// <summary>
/// Rubrique prête au calcul : sa formule (texte lu en base, <c>RubriqueFormules</c>)
/// et ses flags d'assiette. <see cref="Expression"/> nulle = rubrique non
/// calculée par formule (ex. IRG, traité par l'<see cref="IrgCalculator"/>).
/// </summary>
public sealed record RubriqueCalcul(
    string Id,
    NatureRubrique Nature,
    string? Expression,
    bool EstImposable,
    bool EstCotisable,
    int Ordre);

/// <summary>
/// Cotisation à appliquer + son caractère salarial (retenue sur le net) ou
/// patronal (hors net). Seules les salariales entrent dans les retenues et
/// réduisent l'assiette imposable.
/// </summary>
public sealed record CotisationCalcul(CotisationDef Def, bool EstSalariale);

/// <summary>
/// Bundle d'entrée du pipeline, résolu à la date de paie pour un agent. Assemblé
/// par la couche Infrastructure (<c>PayrollReadRepository</c>) ; le pipeline est
/// pur et ne lit jamais la base.
/// </summary>
public sealed record PayrollInput(
    AgentContext Agent,
    string DatePaie,
    IReadOnlyDictionary<string, decimal> Variables,
    IReadOnlyDictionary<string, decimal> SourcesValeur,
    IReadOnlyDictionary<string, string> ClesBareme,
    IReadOnlyList<RubriqueCalcul> Rubriques,
    IReadOnlyList<BaremeValue> Baremes,
    IReadOnlyList<ConditionEligibilite> Conditions,
    IReadOnlyDictionary<string, CritereEligibilite> Criteres,
    IReadOnlyList<CotisationCalcul> Cotisations,
    ProfilFiscal Profil,
    IrgReglePeriode? RegleIrg);

/// <summary>Ligne d'un bulletin — un montant calculé, tracé par son explication (RM-105).</summary>
public sealed record BulletinLigne(
    string RubriqueId,
    NatureRubrique Nature,
    Money Montant,
    bool Imposable,
    bool Cotisable,
    ExplicationLigne Explication);

/// <summary>
/// Bulletin de paie calculé (gains, assiettes, retenues, IRG, net) + journal
/// d'exécution (<see cref="Audit"/>, RM-105 / V4 Tome C vol. 9 §17).
/// </summary>
public sealed record Bulletin(
    IReadOnlyList<BulletinLigne> Lignes,
    Money TotalGains,
    Money AssietteCotisable,
    Money AssietteImposable,
    Money TotalRetenues,
    Money Irg,
    Money Net,
    JournalAudit Audit);
