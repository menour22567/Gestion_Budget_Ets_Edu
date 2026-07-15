using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Domain.Workbench.Services;

/// <summary>
/// Évalueur de conditions d'éligibilité — supporte ET plat (V008) ET DNF
/// (V009, D5). Conditions d'un groupe ETées, groupes OUés, conditions sans
/// groupe communes.
/// </summary>
/// <remarks>
/// Algorithme :
///  1. Récupérer toutes les conditions de la rubrique à la date demandée.
///  2. Séparer conditions communes (GroupeId == null) et conditions groupées.
///  3. Si TOUTES les conditions communes sont satisfaites ET AU MOINS UN groupe
///     a TOUTES ses conditions satisfaites, la rubrique est éligible.
///  4. Sinon, retourner un diagnostic listant les conditions non satisfaites
///     (par sévérité décroissante).
///
/// La résolution effective d'une valeur de critère utilise
/// <see cref="CritereEligibiliteResolver"/> (extensibilité D3 : un nouveau type
/// de source = nouvelle stratégie de résolution, pas de modification du moteur).
/// </remarks>
public sealed class RegleEligibiliteEvaluator
{
    private readonly CritereEligibiliteResolver _critere;

    public RegleEligibiliteEvaluator(CritereEligibiliteResolver critere)
    {
        ArgumentNullException.ThrowIfNull(critere);
        _critere = critere;
    }

    /// <summary>
    /// Évalue l'éligibilité d'une rubrique pour un agent à une date donnée.
    /// </summary>
    /// <param name="rubriqueId">Rubrique cible.</param>
    /// <param name="agent">Snapshot des caractéristiques de l'agent.</param>
    /// <param name="datePaie">Date de paie (pour la résolution temporelle).</param>
    /// <param name="conditions">Toutes les conditions de la rubrique, versionnées.</param>
    /// <param name="criteres">Dictionnaire des critères (pour la résolution de valeur).</param>
    /// <remarks>
    /// La structure DNF est déduite du <c>GroupeId</c> porté par chaque condition —
    /// les en-têtes <c>GroupesEligibilite</c> (sévérité, message, priorité) ne
    /// participent pas à l'évaluation booléenne, ils servent au diagnostic UI.
    /// Ce choix garantit qu'un appelant qui ne charge pas les en-têtes (ex. le
    /// simulateur D8) évalue exactement la même règle que le moteur.
    /// </remarks>
    public ResultatEligibilite Evaluer(
        string rubriqueId,
        AgentContext agent,
        string datePaie,
        IReadOnlyList<ConditionEligibilite> conditions,
        IReadOnlyDictionary<string, CritereEligibilite> criteres)
    {
        ArgumentNullException.ThrowIfNull(rubriqueId);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(datePaie);
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(criteres);

        // 1. Filtre temporel : ne garder que les conditions actives à la date.
        var actives = conditions
            .Where(c => c.RubriqueId == rubriqueId && c.Periode.Contient(datePaie))
            .ToList();
        if (actives.Count == 0)
        {
            // Aucune condition n'est définie pour cette rubrique à cette date.
            // Pas de contrainte = éligible (cf. J3B RM-040 : « toutes les conditions
            // d'éligibilité sont satisfaites » ; vide ⊨ vraie).
            return ResultatEligibilite.Eligible();
        }

        // 2. Sépare communes et groupées.
        var communes = actives.Where(c => c.GroupeId is null).ToList();
        var groupees = actives.Where(c => c.GroupeId is not null).ToList();

        var diagnostics = new List<DiagnosticCondition>();

        // 3. Évalue les conditions communes (ET plat).
        foreach (var cond in communes)
        {
            if (!criteres.TryGetValue(cond.CritereId, out var critere))
            {
                diagnostics.Add(new DiagnosticCondition(
                    cond, false, $"Critère inconnu '{cond.CritereId}'"));
                continue;
            }

            var ok = EvaluerCondition(cond, critere, agent);
            if (!ok)
            {
                diagnostics.Add(new DiagnosticCondition(cond, false, null));
            }
        }

        // Si UNE condition commune n'est pas satisfaite → pas éligible.
        if (diagnostics.Count > 0)
        {
            return ResultatEligibilite.Ineligible(diagnostics);
        }

        // 4. S'il n'y a aucune condition groupée, l'éligibilité est acquise
        //    (ET plat V008 uniquement).
        if (groupees.Count == 0)
        {
            return ResultatEligibilite.Eligible();
        }

        // 5. DNF : au moins un groupe doit avoir TOUTES ses conditions satisfaites.
        var parGroupe = groupees.GroupBy(c => c.GroupeId!, StringComparer.Ordinal).ToList();
        var groupeSatisfaitTrouve = false;
        var diagnosticsGroupes = new List<DiagnosticCondition>();

        foreach (var grp in parGroupe)
        {
            var groupeEligible = true;
            foreach (var cond in grp)
            {
                if (!criteres.TryGetValue(cond.CritereId, out var critere))
                {
                    diagnosticsGroupes.Add(new DiagnosticCondition(
                        cond, false, $"Critère inconnu '{cond.CritereId}'"));
                    groupeEligible = false;
                    continue;
                }
                if (!EvaluerCondition(cond, critere, agent))
                {
                    diagnosticsGroupes.Add(new DiagnosticCondition(cond, false, null));
                    groupeEligible = false;
                }
            }
            if (groupeEligible)
            {
                groupeSatisfaitTrouve = true;
                break; // un seul groupe suffit (OU)
            }
        }

        if (!groupeSatisfaitTrouve)
        {
            return ResultatEligibilite.Ineligible(diagnosticsGroupes);
        }
        return ResultatEligibilite.Eligible();
    }

    /// <summary>
    /// Évalue une condition atomique (résolution de la valeur côté agent puis
    /// application de l'opérateur). Le résolveur de critère est l'extension
    /// D3 : <see cref="CritereEligibiliteResolver"/>.
    /// </summary>
    private bool EvaluerCondition(
        ConditionEligibilite cond,
        CritereEligibilite critere,
        AgentContext agent)
    {
        var valeurAgent = _critere.Resoudre(critere, agent);
        if (valeurAgent is null) return false;
        return Appliquer(cond.Operateur, valeurAgent, cond.Valeur);
    }

    private static bool Appliquer(Operateur op, object valeurAgent, string valeurCondition)
    {
        // Stratégie conservative : on délègue à la résolution typée (string/decimal/int).
        // Si les types ne correspondent pas, on retourne false (condition non satisfaite)
        // plutôt qu'une exception — l'invariant est « condition non satisfaite = pas
        // d'éligibilité », pas « crash ».
        return op switch
        {
            Operateur.Egal => Equals(valeurAgent.ToString(), valeurCondition),
            Operateur.In => valeurCondition.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                          .Contains(valeurAgent.ToString(), StringComparer.Ordinal),
            Operateur.NotIn => !valeurCondition.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                              .Contains(valeurAgent.ToString(), StringComparer.Ordinal),
            Operateur.SuperieurEgal or Operateur.InferieurEgal
                or Operateur.Superieur or Operateur.Inferieur
                => ComparerNumeriques(op, valeurAgent, valeurCondition),
            _ => false
        };
    }

    private static bool ComparerNumeriques(Operateur op, object valeurAgent, string valeurCondition)
    {
        if (!TryToDecimal(valeurAgent, out var gauche)) return false;
        if (!decimal.TryParse(valeurCondition, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var droite)) return false;
        return op switch
        {
            Operateur.SuperieurEgal => gauche >= droite,
            Operateur.InferieurEgal => gauche <= droite,
            Operateur.Superieur => gauche > droite,
            Operateur.Inferieur => gauche < droite,
            _ => false
        };
    }

    private static bool TryToDecimal(object valeur, out decimal d)
    {
        d = 0m;
        return valeur switch
        {
            decimal dec => (d = dec) == dec,
            int i => (d = i) == i,
            long l => (d = l) == l,
            string s => decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out d),
            _ => false
        };
    }
}

/// <summary>
/// Résultat de l'évaluation d'éligibilité (éligible / non, + diagnostics).
/// </summary>
public sealed record ResultatEligibilite(bool EstEligible, IReadOnlyList<DiagnosticCondition> Diagnostics)
{
    public static ResultatEligibilite Eligible() => new(true, Array.Empty<DiagnosticCondition>());
    public static ResultatEligibilite Ineligible(IReadOnlyList<DiagnosticCondition> diagnostics)
        => new(false, diagnostics);
}

/// <summary>
/// Diagnostic d'une condition non satisfaite (utilisé par l'UI Workbench pour
/// afficher « Pourquoi cette rubrique ? »).
/// </summary>
public sealed record DiagnosticCondition(
    ConditionEligibilite Condition,
    bool Satisfaite,
    string? Motif);
