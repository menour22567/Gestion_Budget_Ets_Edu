using Moq;
using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Unit.Workbench.Application;

/// <summary>
/// Tests du chemin « impact réel » de
/// <see cref="SimulerEvolutionReglementaire"/> (D8 / ADR-0007). Couvre
/// uniquement les **validations** du chemin full (paramètres manquants,
/// valeurs invalides, dépendance absente) — le calcul de delta lui-même
/// est validé par les tests d'intégration avec SQLite réelle, où l'engine
/// bout-en-bout peut vraiment propager la VPI à travers le pipeline.
/// Cf. J5L §3.3 — D-S3 (mode dual lite/full).
/// </summary>
public class SimulerImpactReelTests
{
    private static SimulerEvolutionReglementaire.Demande D(
        decimal nouvelleVpi, string dateEffet, string? dateCalcul,
        IReadOnlyList<string>? agentIds = null,
        IReadOnlyList<ConditionEligibilite>? conditions = null,
        IReadOnlyDictionary<string, CritereEligibilite>? criteres = null,
        params (string Eff, string? Fin)[] existantes)
    {
        var periodesExistantes = existantes
            .Select(e => PeriodeReglementaire.Creer(e.Eff, e.Fin))
            .ToList();
        return new SimulerEvolutionReglementaire.Demande(
            RubriqueId: "VALEUR_POINT",
            Description: "Test impact",
            NouvellePeriode: PeriodeReglementaire.Creer(dateEffet, null),
            PeriodesExistantes: periodesExistantes,
            ConditionsApres: conditions,
            Criteres: criteres,
            NouvelleValeurPoint: nouvelleVpi,
            AgentIdsPourImpact: agentIds,
            DateCalcul: dateCalcul);
    }

    [Fact]
    public void Chemin_full_avec_NouvelleValeurPoint_sans_deps_injectees_leve_InvalidOperationException()
    {
        // Constructeur lite (parameterless) utilisé en mode full → échec de
        // configuration, pas une validation métier. Le use case refuse de
        // produire un rapport d'impact (qui exigerait I/O) sans dépendances.
        var useCase = new SimulerEvolutionReglementaire();
        var demande = D(nouvelleVpi: 50m, dateEffet: "2026-01-01", dateCalcul: "2026-01-01");

        var ex = Assert.Throws<InvalidOperationException>(() => useCase.Executer(demande));
        Assert.Contains("CalculerBulletin", ex.Message);
        Assert.Contains("IAgentCarriereRepository", ex.Message);
        Assert.Contains("IBulletinReadRepository", ex.Message);
    }

    [Fact]
    public void Chemin_full_refuse_NouvelleValeurPoint_negative()
    {
        var useCase = SimulerFull();
        var demande = D(nouvelleVpi: -5m, dateEffet: "2026-01-01", dateCalcul: "2026-01-01");

        var r = useCase.Executer(demande);
        Assert.True(r.IsFailure);
        Assert.Contains("strictement positive", r.Error.Message);
    }

    [Fact]
    public void Chemin_full_refuse_NouvelleValeurPoint_nulle()
    {
        var useCase = SimulerFull();
        var demande = D(nouvelleVpi: 0m, dateEffet: "2026-01-01", dateCalcul: "2026-01-01");

        var r = useCase.Executer(demande);
        Assert.True(r.IsFailure);
        Assert.Contains("strictement positive", r.Error.Message);
    }

    [Fact]
    public void Chemin_full_refuse_DateCalcul_absente()
    {
        var useCase = SimulerFull();
        var demande = D(nouvelleVpi: 50m, dateEffet: "2026-01-01", dateCalcul: null);

        var r = useCase.Executer(demande);
        Assert.True(r.IsFailure);
        Assert.Contains("DateCalcul", r.Error.Message);
    }

    [Fact]
    public void Chemin_full_refuse_AgentIdsPourImpact_absent()
    {
        var useCase = SimulerFull();
        var demande = D(nouvelleVpi: 50m, dateEffet: "2026-01-01", dateCalcul: "2026-01-01",
            agentIds: null);

        var r = useCase.Executer(demande);
        Assert.True(r.IsFailure);
        Assert.Contains("AgentIdsPourImpact", r.Error.Message);
    }

    [Fact]
    public void Chemin_full_refuse_AgentIdsPourImpact_vide()
    {
        var useCase = SimulerFull();
        var demande = D(nouvelleVpi: 50m, dateEffet: "2026-01-01", dateCalcul: "2026-01-01",
            agentIds: Array.Empty<string>());

        var r = useCase.Executer(demande);
        Assert.True(r.IsFailure);
        Assert.Contains("AgentIdsPourImpact", r.Error.Message);
    }

    [Fact]
    public void Chemin_full_refuse_ConditionsApres_ou_Criteres_absents()
    {
        var useCase = SimulerFull();
        // ConditionsApres et Criteres null → on ne peut pas évaluer
        // l'éligibilité DNF.
        var demande = D(nouvelleVpi: 50m, dateEffet: "2026-01-01", dateCalcul: "2026-01-01",
            agentIds: new[] { "A-1" }, conditions: null, criteres: null);

        var r = useCase.Executer(demande);
        Assert.True(r.IsFailure);
        Assert.Contains("ConditionsApres", r.Error.Message);
    }

    [Fact]
    public void Chemin_full_applique_la_validation_de_continuite_meme_avec_NouvelleValeurPoint()
    {
        // La validation L-U8 (pas de chevauchement, pas de trou) doit
        // s'appliquer **avant** toute I/O — un simulateur qui tenterait
        // de calculer un impact sur une évolution invalide serait
        // un bug de priorité métier.
        var useCase = SimulerFull();
        var demande = D(nouvelleVpi: 50m, dateEffet: "2011-06-01", dateCalcul: "2011-06-01",
            existantes: new (string Eff, string? Fin)[]
            {
                ("2008-01-01", "2010-12-31"),
                ("2011-01-01", "2014-12-31")  // chevauche avec 2011-06-01
            });

        var r = useCase.Executer(demande);
        Assert.True(r.IsFailure);
        Assert.Contains("continuité", r.Error.Message);
    }

    [Fact]
    public void BaremesOverride_seul_sans_NouvelleValeurPoint_declenche_le_chemin_full_et_n_ecrit_rien()
    {
        // Lot 3.2 (J5M §3 D-B1/D-B5) : BaremesOverride != null déclenche
        // le chemin full, même si NouvelleValeurPoint est null. C'est le
        // scénario « et si je change le forfait DOC_PEDAG ? ».
        // L'agent n'est pas trouvable (mock strict retourne NotFound) → le
        // simulateur l'ignore silencieusement et continue (NbAgents=0).
        var agents = new Mock<IAgentCarriereRepository>(MockBehavior.Strict);
        agents.Setup(a => a.ResoudreAsync("A-INEXISTANT", "2026-01-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<AgentContext>(Error.NotFound("Agent introuvable : 'A-INEXISTANT'.")));
        var useCase = SimulerFullAvec(agents);
        var conditions = new[]
        {
            ConditionEligibilite.Creer("C-IMP", "DOC_PEDAG", "CORPS",
                Operateur.Egal, "PDLP", null,
                PeriodeReglementaire.Creer("2026-01-01", null))
        };
        var criteres = new Dictionary<string, CritereEligibilite>
        {
            ["CORPS"] = CritereEligibilite.Creer("CORPS", "Corps",
                TypeValeurCritere.Enum, SourceResolution.Carriere)
        };
        var baremeOverride = new[]
        {
            BaremeValue.Creer(
                rubriqueId: "DOC_PEDAG",
                dimension: BaremeDimension.Categorie,
                borneInf: "13", borneSup: "13",
                typeValeur: BaremeTypeValeur.Montant,
                valeur: "3000",
                periode: PeriodeReglementaire.Creer("2026-01-01", null))
        };

        var demande = new SimulerEvolutionReglementaire.Demande(
            RubriqueId: "DOC_PEDAG",
            Description: "DOC_PEDAG cat.13 2000 → 3000",
            NouvellePeriode: PeriodeReglementaire.Creer("2026-01-01", null),
            PeriodesExistantes: Array.Empty<PeriodeReglementaire>(),
            ConditionsApres: conditions,
            Criteres: criteres,
            NouvelleValeurPoint: null,   // <-- pas de VPI, juste barème
            AgentIdsPourImpact: new[] { "A-INEXISTANT" },
            DateCalcul: "2026-01-01",
            BaremesOverride: baremeOverride);

        var r = useCase.Executer(demande);
        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        Assert.Equal(0, r.Value.NbAgents);
        Assert.Equal(0m, r.Value.DeltaMinMensuel);  // aucun agent éligible → 0
    }

    /// <summary>
    /// Construit un simulateur « full » avec stubs réels (classes sealed,
    /// non mockables). Les 3 ports de domaine (Agents / Bulletins / Clock)
    /// sont mockés en strict, le use case <see cref="CalculerBulletin"/>
    /// est construit avec ses 5 vraies dépendances (mêmes mocks + un
    /// <see cref="CalculEntreeResolver"/> réel avec un resolver vide).
    /// Les mocks ne sont pas appelés dans la plupart des tests ci-dessus
    /// (échec de validation avant I/O).
    /// </summary>
    private static SimulerEvolutionReglementaire SimulerFull()
    {
        return SimulerFullAvec(new Mock<IAgentCarriereRepository>(MockBehavior.Strict));
    }

    private static SimulerEvolutionReglementaire SimulerFullAvec(Mock<IAgentCarriereRepository> agents)
    {
        var bulletins = new Mock<IBulletinReadRepository>(MockBehavior.Strict);
        var clock = new Mock<IClock>(MockBehavior.Strict);
        var variables = new Mock<IVariableRepository>(MockBehavior.Strict);
        var payroll = new Mock<IPayrollReadRepository>(MockBehavior.Strict);
        var parametres = new Mock<IParametreSystemeRepository>(MockBehavior.Strict);
        var entreeResolver = new PaieEducation.Application.Payroll.Services.CalculEntreeResolver(
            new SourceValeurResolver(new Dictionary<string, PaieEducation.Domain.Workbench.Calculators.ISourceValeurCalculator>()));
        var calcul = new CalculerBulletin(agents.Object, variables.Object, payroll.Object, parametres.Object, entreeResolver);
        return new SimulerEvolutionReglementaire(calcul, agents.Object, bulletins.Object, clock.Object);
    }
}
