using PaieEducation.Application.Workbench.Services;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Shared.Guards;
using PaieEducation.Shared.Results;

namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Use case de simulation d'une évolution réglementaire (D8). Produit un
/// <see cref="RapportImpact"/> en lecture seule — n'écrit rien en base (cf.
/// ADR-0007 D8 : dry-run obligatoire avant commit).
/// </summary>
/// <remarks>
/// Comportement V1 :
///  1. Validation de la continuité temporelle de l'évolution (garde-fou L-U8).
///  2. Si <see cref="Demande.AgentsCandidats"/> ET
///     <see cref="Demande.ConditionsApres"/> ET <see cref="Demande.Criteres"/>
///     sont fournis, compte les agents éligibles à la nouvelle période
///     (preview d'impact). Sinon, <c>NbAgents = 0</c>.
///  3. Le calcul de delta min/max/montant reste un placeholder — il sera
///     branché en Phase 4 sur le moteur de calcul de masse (EligibilityEngine +
///     FormulaEngine + générateur de rappels D9).
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
    /// Échantillon d'agents à évaluer. Optionnel — si null, on retourne NbAgents=0.
    /// </param>
    /// <param name="ConditionsApres">
    /// Conditions de la rubrique qui s'appliqueront à la nouvelle période
    /// (récupérées depuis le repository Workbench). Optionnel.
    /// </param>
    /// <param name="Criteres">Dictionnaire des critères (depuis CriteresEligibilite).
    /// Optionnel — requis si <paramref name="ConditionsApres"/> est fourni.</param>
    public sealed record Demande(
        string RubriqueId,
        string Description,
        PeriodeReglementaire NouvellePeriode,
        IReadOnlyList<PeriodeReglementaire> PeriodesExistantes,
        IReadOnlyList<AgentContext>? AgentsCandidats = null,
        IReadOnlyList<ConditionEligibilite>? ConditionsApres = null,
        IReadOnlyDictionary<string, CritereEligibilite>? Criteres = null);

    private readonly RegleEligibiliteEvaluator _evaluator = new(new CritereEligibiliteResolver());

    /// <summary>
    /// Simule l'évolution et retourne un rapport d'impact (lecture seule).
    /// <see cref="ContinuiteTemporelle"/> est un service statique (sans
    /// dépendance) ; aucune injection n'est nécessaire pour ce use case V1.
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

        // 2. Comptage des agents éligibles (preview d'impact V1).
        // Si les 3 champs optionnels sont fournis, on évalue chaque agent.
        // Sinon, NbAgents = 0 (mode "validation uniquement" — l'utilisateur sait
        // déjà ce qu'il fait et veut juste vérifier la continuité).
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

        // 3. Placeholder — Phase 4. Le delta min/max/montant total sera calculé
        // par le moteur de calcul de masse sur la base des bulletins calculés.
        var rapport = new RapportImpact(
            NbAgents: nbAgents,
            DeltaMinMensuel: 0m,
            DeltaMaxMensuel: 0m,
            MontantTotalMensuel: 0m,
            PeriodeImpactee: demande.NouvellePeriode.DateEffet,
            BulletinsAvertis: 0);

        return Result.Success(rapport);
    }
}
