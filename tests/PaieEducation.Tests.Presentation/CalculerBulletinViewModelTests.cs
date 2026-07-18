using Moq;
using PaieEducation.Application.Payroll.Services;
using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Domain.Calcul.Cotisations;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Workbench.Calculators;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Presentation.Payroll;

namespace PaieEducation.Tests.Presentation;

/// <summary>
/// Tests de <see cref="CalculerBulletinViewModel"/> (Phase 6, tâche 1) —
/// les 3 ports (<see cref="IAgentCarriereRepository"/>,
/// <see cref="IVariableRepository"/>, <see cref="IPayrollReadRepository"/>)
/// sont mockés ; <see cref="CalculerBulletin"/> et
/// <see cref="CalculerBulletinViewModel"/> sont réels — seule la frontière
/// I/O est simulée (même principe que les tests d'intégration, sans SQLite).
/// </summary>
public class CalculerBulletinViewModelTests
{
    private static AgentContext AgentDeTest() => new(
        Filiere: null, Corps: null, Grade: null, Categorie: null, Echelon: null,
        AncienneteAnnees: null, Fonction: null, TypeContrat: null, TypeEtablissement: null,
        OrigineStatutaire: null, Note: null, ValeurPointIndiciaire: null,
        AssietteCotisable: null, AssietteImposable: null);

    [Fact]
    public async Task CalculerAsync_succes_renseigne_le_resultat_detaille()
    {
        var agents = new Mock<IAgentCarriereRepository>();
        agents.Setup(a => a.ResoudreAsync("A-1", "2025-06-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(AgentDeTest()));

        var variables = new Mock<IVariableRepository>();
        variables.Setup(v => v.ResoudreAsync(It.IsAny<AgentContext>(), "2025-06-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyDictionary<string, decimal>>(new Dictionary<string, decimal>()));

        // Input minimal mais valide (aucune rubrique, aucune cotisation, pas
        // de règle IRG) : le pipeline réel produit un bulletin net = 0 DA,
        // suffisant pour prouver que le ViewModel affiche un vrai résultat.
        var payrollInput = new PayrollInput(
            AgentDeTest(), "2025-06-01",
            new Dictionary<string, decimal>(), new Dictionary<string, decimal>(), new Dictionary<string, string>(),
            Array.Empty<RubriqueCalcul>(), Array.Empty<BaremeValue>(), Array.Empty<ConditionEligibilite>(),
            new Dictionary<string, CritereEligibilite>(), Array.Empty<CotisationCalcul>(),
            ProfilFiscal.Standard, RegleIrg: null);

        var payroll = new Mock<IPayrollReadRepository>();
        payroll.Setup(p => p.ChargerAsync(
                It.IsAny<AgentContext>(), "2025-06-01",
                It.IsAny<IReadOnlyDictionary<string, decimal>>(), It.IsAny<IReadOnlyDictionary<string, decimal>>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<ProfilFiscal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(payrollInput));

        var calculerBulletin = new CalculerBulletin(
            agents.Object, variables.Object, payroll.Object, ParametresMock(), EntreeResolverMock());
        var agentsRead = new Mock<IAgentReadRepository>();
        agentsRead.Setup(a => a.ListerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<AgentResume>>(
                new[] { new AgentResume("A-1", "MAT-1", "Dupont", "Jean") }));
        var dialogs = new Mock<IDialogService>();
        var vm = new CalculerBulletinViewModel(calculerBulletin, agentsRead.Object, dialogs.Object)
        {
            AgentId = "A-1",
            DatePaie = "2025-06-01",
        };

        await vm.CalculerCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.Equal(0m, vm.Net);
        Assert.Equal(0m, vm.TotalGains);
        Assert.Empty(vm.Lignes);
        Assert.Empty(vm.Audit);
        Assert.NotNull(vm.Bulletin);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChargerAgentsAsync_peuple_le_selecteur()
    {
        var calculerBulletin = new CalculerBulletin(
            new Mock<IAgentCarriereRepository>().Object, new Mock<IVariableRepository>().Object,
            new Mock<IPayrollReadRepository>().Object, ParametresMock(), EntreeResolverMock());
        var agentsRead = new Mock<IAgentReadRepository>();
        agentsRead.Setup(a => a.ListerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<AgentResume>>(
                new[] { new AgentResume("A-1", "MAT-1", "Dupont", "Jean") }));
        var dialogs = new Mock<IDialogService>();
        var vm = new CalculerBulletinViewModel(calculerBulletin, agentsRead.Object, dialogs.Object);

        await vm.ChargerAgentsCommand.ExecuteAsync(null);

        Assert.Single(vm.Agents);
        Assert.Equal("MAT-1 — Dupont Jean", vm.Agents[0].Libelle);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CalculerAsync_echec_affiche_une_erreur_sans_renseigner_le_resultat()
    {
        var agents = new Mock<IAgentCarriereRepository>();
        agents.Setup(a => a.ResoudreAsync("A-INEXISTANT", "2025-06-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<AgentContext>(Error.NotFound("Agent introuvable : 'A-INEXISTANT'.")));

        var calculerBulletin = new CalculerBulletin(
            agents.Object, new Mock<IVariableRepository>().Object, new Mock<IPayrollReadRepository>().Object,
            ParametresMock(), EntreeResolverMock());
        var agentsRead = new Mock<IAgentReadRepository>();
        agentsRead.Setup(a => a.ListerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<AgentResume>>(Array.Empty<AgentResume>()));
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(d => d.ShowErrorAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        var vm = new CalculerBulletinViewModel(calculerBulletin, agentsRead.Object, dialogs.Object)
        {
            AgentId = "A-INEXISTANT",
            DatePaie = "2025-06-01",
        };

        await vm.CalculerCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.Empty(vm.Lignes);
        Assert.Null(vm.Bulletin);
        dialogs.Verify(d => d.ShowErrorAsync(It.Is<string>(m => m.Contains("introuvable"))), Times.Once);
    }

    private static IParametreSystemeRepository ParametresMock()
    {
        var mock = new Mock<IParametreSystemeRepository>();
        mock.Setup(p => p.LireModeArrondiAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(ModeArrondi.DinarPlusProche));
        mock.Setup(p => p.LireDecimalOuDefautAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, decimal defaut, string _, CancellationToken _) => Result.Success(defaut));
        mock.Setup(p => p.LireDecimalObligatoireAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string cle, string _, CancellationToken _) => cle switch
            {
                "BASE_PAPP" => Result.Success(0.40m),
                "NOTE_MAX_PAPP" => Result.Success(20m),
                "SEUIL_EXONERATION_IRG" => Result.Success(30000m),
                "PLAFOND_LISSAGE_GENERAL" => Result.Success(35000m),
                _ => Result.Failure<decimal>(Error.NotFound($"Paramètre obligatoire « {cle} » absent.")),
            });
        return mock.Object;
    }

    /// <summary>
    /// <see cref="CalculEntreeResolver"/> avec le resolver réel indexant le
    /// calculateur <c>NOTATION_AGENT</c> (lit <see cref="AgentContext.Note"/>),
    /// reproduisant le contrat de production sans SQLite.
    /// </summary>
    private static CalculEntreeResolver EntreeResolverMock()
    {
        var calculators = new Dictionary<string, ISourceValeurCalculator>(StringComparer.OrdinalIgnoreCase)
        {
            ["NOTATION_AGENT"] = new NotationAgentCalculator(),
        };
        return new CalculEntreeResolver(new SourceValeurResolver(calculators));
    }
}
