using Moq;
using PaieEducation.Application.Payroll.Services;
using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Domain.Calcul.Cotisations;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Workbench.Calculators;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Presentation.Payroll;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Presentation;

/// <summary>
/// Tests de <see cref="ValiderBulletinViewModel"/> (Phase 6, tâche 3) — les
/// 4 ports (<see cref="IAgentCarriereRepository"/>, <see cref="IVariableRepository"/>,
/// <see cref="IPayrollReadRepository"/>, <see cref="IBulletinRepository"/>)
/// sont mockés ; <see cref="ValiderBulletin"/> et le ViewModel sont réels.
/// </summary>
public class ValiderBulletinViewModelTests
{
    private static AgentContext AgentDeTest() => new(
        Filiere: null, Corps: null, Grade: null, Categorie: null, Echelon: null,
        AncienneteAnnees: null, Fonction: null, TypeContrat: null, TypeEtablissement: null,
        OrigineStatutaire: null, Note: null, ValeurPointIndiciaire: null,
        AssietteCotisable: null, AssietteImposable: null);

    [Fact]
    public async Task ValiderAsync_succes_renseigne_le_resultat()
    {
        var agents = new Mock<IAgentCarriereRepository>();
        agents.Setup(a => a.ResoudreAsync("A-1", "2025-06-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(AgentDeTest()));

        var variables = new Mock<IVariableRepository>();
        variables.Setup(v => v.ResoudreAsync(It.IsAny<AgentContext>(), "2025-06-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyDictionary<string, decimal>>(new Dictionary<string, decimal>()));

        var payrollInput = new PayrollInput(
            AgentDeTest(), "2025-06-01",
            new Dictionary<string, decimal>(), new Dictionary<string, decimal>(), new Dictionary<string, string>(),
            Array.Empty<RubriqueCalcul>(), Array.Empty<BaremeValue>(), Array.Empty<ConditionEligibilite>(),
            new Dictionary<string, CritereEligibilite>(), Array.Empty<CotisationCalcul>(),
            ProfilFiscal.Standard, RegleIrg: null,
            // Lot 2.1 : dépendances vides — les tests ViewModel n'exercent pas
            // l'ordre topologique.
            Array.Empty<DependanceArete>());

        var payroll = new Mock<IPayrollReadRepository>();
        payroll.Setup(p => p.ChargerAvecBaremesOverrideAsync(
                It.IsAny<AgentContext>(), "2025-06-01",
                It.IsAny<IReadOnlyDictionary<string, decimal>>(), It.IsAny<IReadOnlyDictionary<string, decimal>>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<ProfilFiscal>(),
                It.IsAny<IReadOnlyList<PaieEducation.Domain.Workbench.ValueObjects.BaremeValue>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(payrollInput));

        var bulletins = new Mock<IBulletinRepository>();
        bulletins.Setup(b => b.ValiderAsync("A-1", It.IsAny<BulletinSnapshot>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success("BUL-NOUVEAU"));

        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);

        var calculerBulletin = new ValiderBulletin(
            CalculerBulletinAvec(agents, variables, payroll), bulletins.Object, clock.Object);
        var dialogs = new Mock<IDialogService>();
        var vm = new ValiderBulletinViewModel(calculerBulletin, AgentsReadVide(), dialogs.Object)
        {
            AgentId = "A-1",
            DatePaie = "2025-06-01",
        };

        await vm.ValiderCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.NotNull(vm.Resultat);
        Assert.Contains("BUL-NOUVEAU", vm.Resultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ValiderAsync_deja_valide_affiche_une_erreur()
    {
        var agents = new Mock<IAgentCarriereRepository>();
        agents.Setup(a => a.ResoudreAsync("A-1", "2025-06-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<AgentContext>(Error.NotFound("Agent introuvable : 'A-1'.")));

        var calculerBulletin = new CalculerBulletin(
            agents.Object, new Mock<IVariableRepository>().Object, new Mock<IPayrollReadRepository>().Object,
            ParametresMock(), EntreeResolverMock());
        var validerBulletin = new ValiderBulletin(
            calculerBulletin, new Mock<IBulletinRepository>().Object, new Mock<IClock>().Object);
        var dialogs = new Mock<IDialogService>();

        var vm = new ValiderBulletinViewModel(validerBulletin, AgentsReadVide(), dialogs.Object)
        {
            AgentId = "A-1",
            DatePaie = "2025-06-01",
        };

        await vm.ValiderCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.Null(vm.Resultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.Is<string>(m => m.Contains("introuvable"))), Times.Once);
    }

    private static CalculerBulletin CalculerBulletinAvec(
        Mock<IAgentCarriereRepository> agents, Mock<IVariableRepository> variables, Mock<IPayrollReadRepository> payroll)
        => new CalculerBulletin(agents.Object, variables.Object, payroll.Object, ParametresMock(), EntreeResolverMock());

    /// <summary>Sélecteur d'agent vide — ces tests posent <c>AgentId</c> directement, sans passer par le ComboBox.</summary>
    private static IAgentReadRepository AgentsReadVide()
    {
        var mock = new Mock<IAgentReadRepository>();
        mock.Setup(a => a.ListerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<AgentResume>>(Array.Empty<AgentResume>()));
        return mock.Object;
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
