using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Application.Workbench.Services;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Shared.Guards;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Use case de simulation d'une évolution réglementaire (D8). Produit un
/// <see cref="RapportImpact"/> en lecture seule — n'écrit rien en base (cf.
/// ADR-0007 D8 : dry-run obligatoire avant commit).
/// </summary>
/// <remarks>
/// <para><b>Deux chemins d'exécution :</b></para>
/// <list type="bullet">
///   <item>
///     <term>Lite (parameterless)</term>
///     <description>Quand <see cref="Demande.NouvelleValeurPoint"/> est
///     <c>null</c> : validation de continuité (L-U8) + comptage des agents
///     éligibles (DNF). Aucune I/O, aucun port requis. C'est le mode utilisé
///     par les 7 tests unitaires existants (régression-safe).</description>
///   </item>
///   <item>
///     <term>Full (deps injectées)</term>
///     <description>Quand <see cref="Demande.NouvelleValeurPoint"/> est fourni
///     ET que les dépendances <see cref="CalculerBulletin"/> + port agent +
///     port bulletins + horloge sont injectées : calcul d'<b>impact réel</b>
///     (delta min/max/total sur le net des bulletins simulés) +
///     <see cref="RapportImpact.BulletinsAvertis"/> pour la rétroactivité.
///     <see cref="Demande.AgentIdsPourImpact"/> est requis dans ce mode.</description>
///   </item>
/// </list>
/// <para>Si <see cref="Demande.NouvelleValeurPoint"/> est fourni sans les
/// dépendances, le use case lève <see cref="InvalidOperationException"/> — un
/// dry-run d'impact ne peut pas tourner sans I/O, c'est un échec de
/// configuration, pas une validation métier.</para>
/// <para>Le périmètre est verrouillé à la VPI (ValeurPoint) pour ce lot :
/// cf. J5L §3.1 (D-S1) — extension à d'autres rubriques (paramètres, barèmes)
/// dans des lots ultérieurs, en suivant le même patron « override ».</para>
/// </remarks>
public sealed class SimulerEvolutionReglementaire
{
    /// <summary>Description d'une évolution réglementaire à simuler.</summary>
    /// <param name="RubriqueId">Rubrique concernée (ex. <c>"QUALIF"</c>).</param>
    /// <param name="Description">Description textuelle libre de l'évolution.</param>
    /// <param name="NouvellePeriode">Période d'application proposée.</param>
    /// <param name="PeriodesExistantes">
    /// Périodes déjà présentes en base pour cette rubrique, à comparer avec
    /// la nouvelle (continuité temporelle).
    /// </param>
    /// <param name="AgentsCandidats">
    /// Échantillon d'agents à évaluer. Optionnel — si null, NbAgents=0.
    /// Utilisé par le chemin lite (éligibilité) et comme fallback si
    /// <see cref="AgentIdsPourImpact"/> n'est pas fourni.
    /// </param>
    /// <param name="ConditionsApres">
    /// Conditions de la rubrique qui s'appliqueront à la nouvelle période.
    /// Optionnel.
    /// </param>
    /// <param name="Criteres">Dictionnaire des critères (depuis CriteresEligibilite).
    /// Optionnel — requis si <paramref name="ConditionsApres"/> est fourni.</param>
    /// <param name="NouvelleValeurPoint">
    /// VPI hypothétique pour le calcul d'impact réel (chemin full). <c>null</c>
    /// = chemin lite (deltas à 0). Cf. J5L §3.2 (D-S2). Doit être &gt; 0.
    /// </param>
    /// <param name="AgentIdsPourImpact">
    /// Liste d'IDs d'agents pour le calcul d'impact réel. <c>null</c> en lite.
    /// Si fourni, chaque agent est résolu via
    /// <see cref="IAgentCarriereRepository.ResoudreAsync"/>, son éligibilité
    /// est évaluée, et — s'il est éligible — son bulletin est recalculé
    /// deux fois (VPI actuelle, VPI hypothétique) pour mesurer le delta réel.
    /// </param>
    /// <param name="DateCalcul">
    /// Date à laquelle l'évolution est simulée (« aujourd'hui »). <c>null</c>
    /// en lite. Sert à déterminer la rétroactivité :
    /// <c>NouvellePeriode.DateEffet &lt; DateCalcul</c> ⇒ bulletins validés
    /// entre la date d'effet et aujourd'hui = candidats au rappel (D9).
    /// </param>
    public sealed record Demande(
        string RubriqueId,
        string Description,
        PeriodeReglementaire NouvellePeriode,
        IReadOnlyList<PeriodeReglementaire> PeriodesExistantes,
        IReadOnlyList<AgentContext>? AgentsCandidats = null,
        IReadOnlyList<ConditionEligibilite>? ConditionsApres = null,
        IReadOnlyDictionary<string, CritereEligibilite>? Criteres = null,
        decimal? NouvelleValeurPoint = null,
        IReadOnlyList<string>? AgentIdsPourImpact = null,
        string? DateCalcul = null);

    private readonly RegleEligibiliteEvaluator _evaluator = new(new CritereEligibiliteResolver());
    private readonly CalculerBulletin? _calcul;
    private readonly IAgentCarriereRepository? _agents;
    private readonly IBulletinReadRepository? _bulletins;
    private readonly IClock? _clock;

    /// <summary>Constructeur lite (parameterless) — pas de deps, pas d'I/O.</summary>
    public SimulerEvolutionReglementaire()
    {
    }

    /// <summary>Constructeur full — toutes les deps pour le chemin impact réel.</summary>
    /// <remarks>
    /// Si <see cref="Demande.NouvelleValeurPoint"/> est fourni, les 4 deps sont
    /// <b>obligatoires</b> : le calcul d'impact ne peut pas tourner sans
    /// accès à l'agent carrière, au moteur de calcul, aux bulletins et à
    /// l'horloge. Un null dans ce contexte lève <see cref="InvalidOperationException"/>
    /// — c'est un échec de configuration, pas un échec métier.
    /// </remarks>
    public SimulerEvolutionReglementaire(
        CalculerBulletin calcul,
        IAgentCarriereRepository agents,
        IBulletinReadRepository bulletins,
        IClock clock)
    {
        _calcul = calcul ?? throw new ArgumentNullException(nameof(calcul));
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _bulletins = bulletins ?? throw new ArgumentNullException(nameof(bulletins));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// Simule l'évolution et retourne un rapport d'impact (lecture seule).
    /// </summary>
    public Result<RapportImpact> Executer(Demande demande)
    {
        ArgumentNullException.ThrowIfNull(demande);
        Guard.AgainstNullOrWhiteSpace(demande.RubriqueId);

        // 1. Validation de la continuité temporelle — garde-fou L-U8.
        var aValider = new List<(string Cle, PeriodeReglementaire Periode)>(demande.PeriodesExistantes.Count + 1);
        aValider.Add((demande.RubriqueId, demande.NouvellePeriode));
        foreach (var p in demande.PeriodesExistantes)
        {
            aValider.Add((demande.RubriqueId, p));
        }
        var validation = ContinuiteTemporelle.Valider(aValider);
        if (validation.IsFailure)
        {
            return Result.Failure<RapportImpact>(
                Error.Validation($"Évolution refusée (continuité temporelle) : {validation.Error.Message}"));
        }

        // 2. Chemin full (impact réel) — exige NouvelleValeurPoint + 4 deps.
        if (demande.NouvelleValeurPoint is { } vpiSimulee)
        {
            return ExecuterCheminFull(demande, vpiSimulee);
        }

        // 3. Chemin lite — backward compat avec les 7 tests unitaires existants.
        return ExecuterCheminLite(demande);
    }

    /// <summary>
    /// Chemin lite : comptage d'agents éligibles (DNF), deltas à 0, pas
    /// d'impact réel. Mode « validation de continuité uniquement ».
    /// </summary>
    private Result<RapportImpact> ExecuterCheminLite(Demande demande)
    {
        int nbAgents = 0;
        if (demande.AgentsCandidats is not null
            && demande.ConditionsApres is not null
            && demande.Criteres is not null)
        {
            foreach (var agent in demande.AgentsCandidats)
            {
                var r = _evaluator.Evaluer(
                    demande.RubriqueId, agent, demande.NouvellePeriode.DateEffet,
                    demande.ConditionsApres, demande.Criteres);
                if (r.EstEligible) nbAgents++;
            }
        }

        return Result.Success(new RapportImpact(
            NbAgents: nbAgents,
            DeltaMinMensuel: 0m,
            DeltaMaxMensuel: 0m,
            MontantTotalMensuel: 0m,
            PeriodeImpactee: demande.NouvellePeriode.DateEffet,
            BulletinsAvertis: 0));
    }

    /// <summary>
    /// Chemin full : impact réel sur le net des bulletins simulés +
    /// BulletinsAvertis pour la rétroactivité. C'est le calcul promis par
    /// ADR-0007 D8 et illustré par le mockup J3I §7.2.
    /// </summary>
    private Result<RapportImpact> ExecuterCheminFull(Demande demande, decimal vpiSimulee)
    {
        if (vpiSimulee <= 0m)
            return Result.Failure<RapportImpact>(
                Error.Validation($"NouvelleValeurPoint doit être strictement positive (reçu : {vpiSimulee})."));

        if (_calcul is null || _agents is null || _bulletins is null || _clock is null)
        {
            throw new InvalidOperationException(
                "SimulerEvolutionReglementaire : le calcul d'impact réel (NouvelleValeurPoint fourni) "
                + "exige les 4 dépendances injectées (CalculerBulletin, IAgentCarriereRepository, "
                + "IBulletinReadRepository, IClock). Utiliser le constructeur full.");
        }

        if (demande.DateCalcul is null)
            return Result.Failure<RapportImpact>(
                Error.Validation(
                    "DateCalcul est obligatoire pour le calcul d'impact réel (détermine la rétroactivité)."));
        if (demande.AgentIdsPourImpact is null || demande.AgentIdsPourImpact.Count == 0)
            return Result.Failure<RapportImpact>(
                Error.Validation(
                    "AgentIdsPourImpact est obligatoire pour le calcul d'impact réel (sinon NbAgents reste à 0)."));
        if (demande.ConditionsApres is null || demande.Criteres is null)
            return Result.Failure<RapportImpact>(
                Error.Validation(
                    "ConditionsApres et Criteres sont obligatoires pour le calcul d'impact réel."));

        // Date de paie = DateCalcul (c'est la date à laquelle la VPI simulée
        // s'applique, et à laquelle on simule le bulletin). Pour les agents
        // dont la carrière ne couvre pas cette date, l'agent est ignoré
        // (RésoudreAsync renverra NotFound, on continue sans lui).
        var datePaie = demande.DateCalcul;

        decimal? deltaMin = null, deltaMax = null;
        decimal montantTotal = 0m;
        int nbAgentsEligibles = 0;

        foreach (var agentId in demande.AgentIdsPourImpact)
        {
            // a) Résolution de l'agent carrière à la date de paie.
            var agentCtx = _agents.ResoudreAsync(agentId, datePaie).GetAwaiter().GetResult();
            if (agentCtx.IsFailure) continue;

            // b) Évaluation de l'éligibilité DNF (réutilise le même evaluator
            //    que le chemin lite — pas de duplication de la règle métier).
            var eligibilite = _evaluator.Evaluer(
                demande.RubriqueId, agentCtx.Value, demande.NouvellePeriode.DateEffet,
                demande.ConditionsApres, demande.Criteres);
            if (!eligibilite.EstEligible) continue;
            nbAgentsEligibles++;

            // c) Calcul du bulletin avec la VPI actuelle (lecture DB).
            var baseline = _calcul.ExecuterAsync(new CalculerBulletin.Demande(
                AgentId: agentId, DatePaie: datePaie)).GetAwaiter().GetResult();
            if (baseline.IsFailure) continue;

            // d) Calcul du bulletin avec la VPI hypothétique (override).
            var simule = _calcul.ExecuterAsync(new CalculerBulletin.Demande(
                AgentId: agentId, DatePaie: datePaie, VpiOverride: vpiSimulee))
                .GetAwaiter().GetResult();
            if (simule.IsFailure) continue;

            // e) Delta sur le net (proxy simple et représentatif — la plupart
            //    des rubriques sont proportionnelles au TRT, lui-même
            //    proportionnel à la VPI ; le delta net capte la propagation
            //    complète y compris cotisations et IRG).
            var delta = simule.Value.Net.Amount - baseline.Value.Net.Amount;

            // On ignore les deltas nuls (VPI inchangée ne change rien —
            // sécurité contre une mauvaise saisie de NouvelleValeurPoint).
            if (delta == 0m) continue;

            if (deltaMin is null || delta < deltaMin) deltaMin = delta;
            if (deltaMax is null || delta > deltaMax) deltaMax = delta;
            montantTotal += delta;
        }

        // f) BulletinsAvertis : nombre de bulletins validés dans la période
        //    [NouvellePeriode.DateEffet, DateCalcul[ si rétroactive. 0 sinon
        //    (évolution future) ou si pas rétroactive.
        int bulletinsAvertis = 0;
        if (string.CompareOrdinal(demande.NouvellePeriode.DateEffet, demande.DateCalcul) < 0)
        {
            var dateFinPeriode = _clock.Today.ToString("yyyy-MM-dd");
            var count = _bulletins.CompterPourPeriodeAsync(
                demande.NouvellePeriode.DateEffet, dateFinPeriode).GetAwaiter().GetResult();
            if (count.IsSuccess) bulletinsAvertis = count.Value;
        }

        return Result.Success(new RapportImpact(
            NbAgents: nbAgentsEligibles,
            DeltaMinMensuel: deltaMin ?? 0m,
            DeltaMaxMensuel: deltaMax ?? 0m,
            MontantTotalMensuel: montantTotal,
            PeriodeImpactee: demande.NouvellePeriode.DateEffet,
            BulletinsAvertis: bulletinsAvertis));
    }
}
