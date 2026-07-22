using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Domain.Calcul.Formules;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Shared.Results;

namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Use case (P10, FormulaEditor avancé) : simule un bulletin pour un agent
/// témoin en remplaçant **temporairement** la formule d'une rubrique par
/// l'expression saisie dans l'éditeur. Ne modifie pas la base.
/// </summary>
/// <remarks>
/// <para>Le filet de sécurité « agent témoin » est l'argument fort de P10 :
/// une formule syntaxiquement valide peut rester sémantiquement fausse (mauvaise
/// variable, opérateur, dimension) et seules les variables résolues par le
/// pipeline sur un cas réel la démasquent.</para>
/// <para>Pipeline : on réutilise <see cref="CalculerBulletin.ResoudreAsync"/>
/// pour obtenir le <c>PayrollInput</c> canonique (variables résolues, barèmes
/// chargés, etc.), on remplace la rubrique cible par une nouvelle
/// <see cref="RubriqueCalcul"/> avec l'expression override, puis on relance
/// <see cref="CalculationPipeline.Calculer"/>.</para>
/// </remarks>
public sealed class SimulerBulletinPourFormule
{
    private readonly CalculerBulletin _calculerBulletin;
    private readonly IParametreSystemeRepository _parametres;
    private readonly IPayrollReadRepository _payroll;

    public SimulerBulletinPourFormule(
        CalculerBulletin calculerBulletin,
        IParametreSystemeRepository parametres,
        IPayrollReadRepository payroll)
    {
        _calculerBulletin = calculerBulletin ?? throw new ArgumentNullException(nameof(calculerBulletin));
        _parametres = parametres ?? throw new ArgumentNullException(nameof(parametres));
        _payroll = payroll ?? throw new ArgumentNullException(nameof(payroll));
    }

    /// <summary>Demande de simulation.</summary>
    /// <param name="AgentId">Identifiant de l'agent témoin (chargé via
    /// <c>IAgentCarriereRepository</c>).</param>
    /// <param name="DatePaie">Date de paie au format ISO (AAAA-MM-JJ).</param>
    /// <param name="RubriqueIdOverride">Code de la rubrique dont la formule
    /// est en cours d'édition (ex. <c>"QUALIF"</c>).</param>
    /// <param name="ExpressionOverride">Formule saisie par l'utilisateur.
    /// Validée par <see cref="FormulaParser"/> avant tout calcul — toute
    /// expression invalide est rejetée sans toucher au pipeline.</param>
    public sealed record Demande(
        string AgentId,
        string DatePaie,
        string RubriqueIdOverride,
        string ExpressionOverride);

    /// <summary>Résultat de la simulation : bulletin override + bulletin baseline.</summary>
    /// <param name="Bulletin">Bulletin calculé avec la formule override.</param>
    /// <param name="BulletinBaseline">Bulletin calculé sans override (formule
    /// officielle de la base), pour affichage côte à côte dans l'UI.</param>
    /// <param name="DeltaNet">Différence de net <c>override − baseline</c>
    /// (DA). Pratique pour faire ressortir l'effet de la formule en un coup
    /// d'œil.</param>
    public sealed record ResultatSimulation(
        Bulletin Bulletin,
        Bulletin BulletinBaseline,
        decimal DeltaNet);

    public async Task<Result<ResultatSimulation>> ExecuterAsync(
        Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);

        // 1) Valider la syntaxe de l'override (on ne lance pas un calcul
        //    complet sur une expression invalide — message clair, exit tôt).
        var parse = FormulaParser.Parser(demande.ExpressionOverride);
        if (parse.IsFailure)
            return Result.Failure<ResultatSimulation>(
                Error.Validation($"Formule invalide : {parse.Error.Message}"));

        // 2) Obtenir le baseline canonique via le use case pilote (réutilise
        //    toute l'orchestration existante — variables, sources, barèmes,
        //    arrondi paramétré, etc.).
        var baseline = await _calculerBulletin.ResoudreAsync(
            new CalculerBulletin.Demande(
                AgentId: demande.AgentId,
                DatePaie: demande.DatePaie),
            ct);
        if (baseline.IsFailure)
            return Result.Failure<ResultatSimulation>(baseline.Error);

        var (inputBase, bulletinBase) = baseline.Value;

        // 3) Construire un nouveau PayrollInput avec la rubrique cible
        //    remplacée par une RubriqueCalcul portant l'expression override.
        //    Si la rubrique n'existe pas dans le calcul chargé (ex. elle est
        //    inéligible pour cet agent), on l'**ajoute** en tête avec les
        //    flags par défaut de la base ; sinon on remplace.
        var rubriquesOverride = AppliquerOverride(
            inputBase.Rubriques, demande.RubriqueIdOverride, demande.ExpressionOverride);

        var inputOverride = inputBase with { Rubriques = rubriquesOverride };

        // 4) Recharger le mode d'arrondi + seuils IRG depuis Parametres et
        //    relancer le pipeline sur l'input override. On duplique la
        //    séquence 4 lignes de CalculerBulletin (paramétrage versionné),
        //    c'est la seule manière d'isoler le calcul de la simulation de
        //    l'orchestration du use case pilote — et ça permet à terme
        //    d'autres what-if (Lot 4 plan d'audit, J5L §3.2 D-S2 : on
        //    bénéficie gratuitement de VpiOverride + BaremesOverride + le
        //    nouveau FormulesOverride).
        var mode = await _parametres.LireModeArrondiAsync(demande.DatePaie, ct);
        if (mode.IsFailure)
            return Result.Failure<ResultatSimulation>(mode.Error);
        var seuilExo = await _parametres.LireDecimalObligatoireAsync("SEUIL_EXONERATION_IRG", demande.DatePaie, ct);
        if (seuilExo.IsFailure)
            return Result.Failure<ResultatSimulation>(seuilExo.Error);
        var plafondLiss = await _parametres.LireDecimalObligatoireAsync("PLAFOND_LISSAGE_GENERAL", demande.DatePaie, ct);
        if (plafondLiss.IsFailure)
            return Result.Failure<ResultatSimulation>(plafondLiss.Error);

        var pipeline = new CalculationPipeline(
            new ArrondiService(mode.Value), seuilExo.Value, plafondLiss.Value);
        var calculeOverride = pipeline.Calculer(inputOverride);
        if (calculeOverride.IsFailure)
            return Result.Failure<ResultatSimulation>(calculeOverride.Error);

        var bulletinOverride = calculeOverride.Value;
        var delta = bulletinOverride.Net.Amount - bulletinBase.Net.Amount;

        return Result.Success(new ResultatSimulation(
            Bulletin: bulletinOverride,
            BulletinBaseline: bulletinBase,
            DeltaNet: delta));
    }

    private static IReadOnlyList<RubriqueCalcul> AppliquerOverride(
        IReadOnlyList<RubriqueCalcul> rubriques,
        string rubriqueIdOverride,
        string expressionOverride)
    {
        var trouvee = false;
        var resultat = new List<RubriqueCalcul>(rubriques.Count);
        foreach (var r in rubriques)
        {
            if (string.Equals(r.Id, rubriqueIdOverride, StringComparison.OrdinalIgnoreCase))
            {
                trouvee = true;
                resultat.Add(r with { Expression = expressionOverride });
            }
            else
            {
                resultat.Add(r);
            }
        }

        if (!trouvee)
        {
            // La rubrique n'est pas dans la liste chargée (inéligible pour
            // l'agent témoin, ou pas encore seedée). On l'ajoute avec des
            // flags neutres : imposable + cotisable (Q-02), nature Gain (le
            // cas COTISATION/RETENUE/IMPOT n'est pas attendu pour une édition
            // de formule depuis l'écran rubrique — l'utilisateur édite la
            // définition d'une rubrique, pas une cotisation).
            resultat.Insert(0, new RubriqueCalcul(
                Id: rubriqueIdOverride,
                Nature: NatureRubrique.Gain,
                Expression: expressionOverride,
                EstImposable: true,
                EstCotisable: true,
                Ordre: 0));
        }

        return resultat;
    }
}
