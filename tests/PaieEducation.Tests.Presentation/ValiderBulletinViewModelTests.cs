using Moq;
using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Domain.Calcul.Cotisations;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Domain.Common;
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
            ProfilFiscal.Standard, RegleIrg: null);

        var payroll = new Mock<IPayrollReadRepository>();
        payroll.Setup(p => p.ChargerAsync(
                It.IsAny<AgentContext>(), "2025-06-01",
                It.IsAny<IReadOnlyDictionary<string, decimal>>(), It.IsAny<IReadOnlyDictionary<string, decimal>>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<ProfilFiscal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(payrollInput));

        var bulletins = new Mock<IBulletinRepository>();
        bulletins.Setup(b => b.ValiderAsync("A-1", It.IsAny<BulletinSnapshot>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success("BUL-NOUVEAU"));

        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);

        var validerBulletin = new ValiderBulletin(agents.Object, variables.Object, payroll.Object, bulletins.Object, clock.Object);
        var dialogs = new Mock<IDialogService>();
        var vm = new ValiderBulletinViewModel(validerBulletin, dialogs.Object)
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

        var validerBulletin = new ValiderBulletin(
            agents.Object, new Mock<IVariableRepository>().Object, new Mock<IPayrollReadRepository>().Object,
            new Mock<IBulletinRepository>().Object, new Mock<IClock>().Object);
        var dialogs = new Mock<IDialogService>();

        var vm = new ValiderBulletinViewModel(validerBulletin, dialogs.Object)
        {
            AgentId = "A-1",
            DatePaie = "2025-06-01",
        };

        await vm.ValiderCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.Null(vm.Resultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.Is<string>(m => m.Contains("introuvable"))), Times.Once);
    }
}
