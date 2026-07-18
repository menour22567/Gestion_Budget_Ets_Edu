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
///  3. Évaluer TOUTES les conditions (pas de court-circuit) : le résultat porte
///     l'explication complète — conditions satisfaites ET non satisfaites,
///     valeur attendue et valeur de l'agent (contrat d'explicabilité J4.e § 7.1).
///  4. Éligible ssi toutes les communes sont satisfaites ET (aucun groupe OU au
///     moins un groupe entièrement satisfait).
///
/// La résolution effective d'une valeur de critère utilise
/// <see cref="CritereEligibiliteResolver"/> (extensibilité D3 : un nouveau type
/// de source = nouvelle stratégie de résolution, pas de modification du moteur).
/// Critère inconnu ou non résolu = condition non satisfaite + détail explicable,
/// jamais d'exception, jamais de droit déduit (ADR-0009).
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

        var groupes = new List<ExplicationGroupe>();

        // 3. Évalue TOUTES les conditions communes (ET plat) — même après un
        //    premier échec : l'explication doit être complète (J4.e § 7.1),
        //    l'UI et l'ExplainabilityEngine la consomment sans retraitement.
        var communesOk = true;
        if (communes.Count > 0)
        {
            var explications = new List<ExplicationCondition>(communes.Count);
            foreach (var cond in communes)
            {
                var e = Expliquer(cond, criteres, agent);
                if (!e.Satisfaite) communesOk = false;
                explications.Add(e);
            }
            groupes.Add(new ExplicationGroupe(GroupeId: null, communesOk, explications));
        }

        // 4. DNF : chaque groupe est évalué INTÉGRALEMENT (pas de court-circuit) —
        //    au moins un groupe entièrement satisfait rend la partie DNF vraie.
        var unGroupeSatisfait = false;
        foreach (var grp in groupees.GroupBy(c => c.GroupeId!, StringComparer.Ordinal))
        {
            var explications = new List<ExplicationCondition>();
            var groupeOk = true;
            foreach (var cond in grp)
            {
                var e = Expliquer(cond, criteres, agent);
                if (!e.Satisfaite) groupeOk = false;
                explications.Add(e);
            }
            if (groupeOk) unGroupeSatisfait = true;
            groupes.Add(new ExplicationGroupe(grp.Key, groupeOk, explications));
        }

        // 5. Verdict : communes toutes satisfaites ET (pas de DNF OU un groupe vrai).
        var eligible = communesOk && (groupees.Count == 0 || unGroupeSatisfait);
        return new ResultatEligibilite(eligible, groupes);
    }

    /// <summary>
    /// Évalue une condition atomique et retourne son explication complète
    /// (valeur attendue, valeur de l'agent, verdict). Critère inconnu ou non
    /// résolu = condition non satisfaite avec <see cref="ExplicationCondition.Detail"/>
    /// renseigné — jamais d'exception, jamais de droit déduit (ADR-0009,
    /// principe d'abstention réglementaire).
    /// </summary>
    private ExplicationCondition Expliquer(
        ConditionEligibilite cond,
        IReadOnlyDictionary<string, CritereEligibilite> criteres,
        AgentContext agent)
    {
        if (!criteres.TryGetValue(cond.CritereId, out var critere))
        {
            return new ExplicationCondition(
                cond.Id, cond.CritereId, cond.Operateur, cond.Valeur,
                ValeurAgent: null, Satisfaite: false,
                Detail: $"Critère inconnu '{cond.CritereId}'");
        }

        var valeurAgent = _critere.Resoudre(critere, agent);
        if (valeurAgent is null)
        {
            return new ExplicationCondition(
                cond.Id, cond.CritereId, cond.Operateur, cond.Valeur,
                ValeurAgent: null, Satisfaite: false,
                Detail: $"Critère non résolu : '{cond.CritereId}' absent du dossier agent");
        }

        var ok = Appliquer(cond.Operateur, valeurAgent, cond.Valeur);
        return new ExplicationCondition(
            cond.Id, cond.CritereId, cond.Operateur, cond.Valeur,
            ValeurAgent: Convert.ToString(valeurAgent, System.Globalization.CultureInfo.InvariantCulture),
            Satisfaite: ok, Detail: null);
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
/// Résultat de l'évaluation d'éligibilité avec explication complète (J4.e § 7.1) :
/// chaque groupe évalué — y compris les conditions SATISFAITES — pour que
/// « Pourquoi cette rubrique ? » et l'<c>ExplainabilityEngine</c> consomment le
/// résultat sans retraitement. La <c>Source</c> réglementaire, la sévérité et le
/// message sont joints par les moteurs de suggestion/avertissement (couche
/// au-dessus) à partir des en-têtes <c>GroupesEligibilite</c> — l'évaluateur
/// reste pur et n'en dépend jamais pour le verdict.
/// </summary>
public sealed record ResultatEligibilite(
    bool EstEligible,
    IReadOnlyList<ExplicationGroupe> Groupes)
{
    /// <summary>Éligible sans condition (RM-040 : vide ⊨ vraie).</summary>
    public static ResultatEligibilite Eligible() => new(true, Array.Empty<ExplicationGroupe>());

    /// <summary>
    /// Abstention (ADR-0009) : rubrique non due faute de donnée requise (ex. source
    /// de valeur absente). Non éligible, mais jamais une erreur bloquante — le calcul
    /// poursuit et la rubrique ne produit aucun montant.
    /// </summary>
    public static ResultatEligibilite Abstention(string rubrique)
        => new(false, new[] { new ExplicationGroupe(
            null, false, new[]
            {
                new ExplicationCondition(
                    $"ABSTENTION-{rubrique}", "SOURCE_VALEUR", Operateur.Egal, rubrique,
                    null, false, $"Source de valeur absente : {rubrique}"),
            }) });

    /// <summary>
    /// Vue à plat des conditions non satisfaites, tous groupes confondus
    /// (diagnostic rapide ; l'explication structurée reste <see cref="Groupes"/>).
    /// </summary>
    public IReadOnlyList<ExplicationCondition> ConditionsNonSatisfaites
        => Groupes.SelectMany(g => g.Conditions).Where(c => !c.Satisfaite).ToList();
}

/// <summary>
/// Explication d'un groupe DNF évalué. <see cref="GroupeId"/> <c>null</c> =
/// conditions communes (ET plat, garde-fous partagés V008).
/// </summary>
public sealed record ExplicationGroupe(
    string? GroupeId,
    bool Satisfait,
    IReadOnlyList<ExplicationCondition> Conditions);

/// <summary>
/// Explication d'une condition évaluée — satisfaite ou non. <see cref="ValeurAgent"/>
/// <c>null</c> signifie « critère non résolu » : la condition est non satisfaite et
/// <see cref="Detail"/> explique pourquoi (principe d'abstention, ADR-0009) — le
/// moteur d'avertissements en dérive le diagnostic <c>DONNEE_MANQUANTE</c>.
/// </summary>
public sealed record ExplicationCondition(
    string ConditionId,
    string CritereId,
    Operateur Operateur,
    string ValeurAttendue,
    string? ValeurAgent,
    bool Satisfaite,
    string? Detail);
