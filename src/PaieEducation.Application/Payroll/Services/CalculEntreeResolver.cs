using PaieEducation.Domain.Calcul.Constants;
using PaieEducation.Domain.Workbench.Constants;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Application.Payroll.Services;

/// <summary>
/// Résout automatiquement les entrées de calcul qui étaient fournies à la main
/// par l'appelant : clés de barème (C2.2) et sources de valeur (C2.3), à partir
/// de l'<see cref="AgentContext"/> déjà résolu (dossier agent). But : zéro
/// saisie experte sur le chemin de calcul (A3, B1 de l'audit).
/// </summary>
/// <remarks>
/// Les clés de barème dérivent directement des caractéristiques de carrière
/// (catégorie, échelon, corps, grade, type d'établissement, ancenneté). Les
/// sources de valeur sont résolues via <see cref="ISourceValeurResolver"/>
/// (pattern Open/Closed, ADR-0007 D6) : la notation agent (PAPP) provient de la
/// source <c>NOTATION_AGENT</c> du catalogue, jamais d'une valeur codée. Si la
/// source n'est pas résolvable (note absente), aucune source n'est fournie et le
/// moteur traite PAPP comme non résolu (abstention ADR-0009).
/// <br/>
/// C8.4 — <c>BASE_PAPP</c> et <c>NOTE_MAX_PAPP</c> lus depuis
/// <c>Parametres</c>, plus codés en dur.
/// </remarks>
public sealed class CalculEntreeResolver
{
    private readonly ISourceValeurResolver _resolver;

    public CalculEntreeResolver(ISourceValeurResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    /// <summary>
    /// Clés de barème dérivées de l'agent (dimension → valeur). Couvre les
    /// dimensions consommées par <c>bareme(RUB, DIM)</c> : CATEGORIE, ECHELON,
    /// ANCIENNETE, TYPE_ETABLISSEMENT, CORPS, GRADE.
    /// </summary>
    public IReadOnlyDictionary<string, string> ResoudreClesBareme(AgentContext agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var cles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (agent.Categorie is { } cat) cles[BaremeDimensionKeys.Categorie] = cat.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (agent.Echelon is { } ech) cles[BaremeDimensionKeys.Echelon] = ech.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (agent.AncienneteAnnees is { } anc) cles[BaremeDimensionKeys.Anciennete] = anc.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!string.IsNullOrEmpty(agent.TypeEtablissement)) cles[BaremeDimensionKeys.TypeEtablissement] = agent.TypeEtablissement!;
        if (!string.IsNullOrEmpty(agent.Corps)) cles[BaremeDimensionKeys.Corps] = agent.Corps!;
        if (!string.IsNullOrEmpty(agent.Grade)) cles[BaremeDimensionKeys.Grade] = agent.Grade!;
        return cles;
    }

    /// <summary>
    /// Sources de valeur dérivées de l'agent. Actuellement : notation agent
    /// résolue via <see cref="ISourceValeurResolver"/> sur la source
    /// <c>NOTATION_AGENT</c>, projetée sur la clé PAPP (formule
    /// <c>TRT * valeurSource(PAPP)</c>). La note (sur <paramref name="noteMax"/>) est convertie en taux
    /// de notation (taux = note / <paramref name="noteMax"/> * <paramref name="basePapp"/>) ; si la source
    /// n'est pas résolvable, aucune source n'est fournie (abstention ADR-0009).
    /// </summary>
    public IReadOnlyDictionary<string, decimal> ResoudreSourcesValeur(
        AgentContext agent, string datePaie, decimal basePapp, decimal noteMax)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(datePaie);

        var sources = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        var note = _resolver.Resoudre(SourceValeur.Creer(SourceValeurCodes.NotationAgent, "Note de l'agent"), agent, datePaie);
        if (note.IsSuccess && note.Value is decimal valeurNote)
        {
            // La formule PAPP consomme valeurSource(PAPP) = taux de notation (0..40 %).
            sources[SourceValeurCodes.Papp] = valeurNote / noteMax * basePapp;
        }

        return sources;
    }

    /// <summary>
    /// Surcharge par défaut pour rétro-compatibilité (tests, usage sans paramètres DB).
    /// Valeurs par défaut : basePapp = 40 %, noteMax = 20 (barème PAPP J3C §2).
    /// </summary>
    public IReadOnlyDictionary<string, decimal> ResoudreSourcesValeur(AgentContext agent, string datePaie)
        => ResoudreSourcesValeur(agent, datePaie, basePapp: 0.40m, noteMax: 20m);
}
