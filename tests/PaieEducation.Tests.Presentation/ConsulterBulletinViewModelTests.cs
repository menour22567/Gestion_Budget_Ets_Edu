using Moq;
using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Domain.Calcul.Audit;
using PaieEducation.Domain.Calcul.Cotisations;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Rappels;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Shared.Money;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Presentation.Payroll;
using PaieEducation.Reporting.UseCases;

namespace PaieEducation.Tests.Presentation;

/// <summary>
/// Tests de <see cref="ConsulterBulletinViewModel"/> (Phase 6, tâche 3) —
/// <see cref="IBulletinReadRepository"/> mocké ; <see cref="ConsulterBulletin"/>
/// et le ViewModel sont réels.
/// </summary>
public class ConsulterBulletinViewModelTests
{
    private static AgentContext AgentDeTest() => new(
        Filiere: null, Corps: null, Grade: null, Categorie: null, Echelon: null,
        AncienneteAnnees: null, Fonction: null, TypeContrat: null, TypeEtablissement: null,
        OrigineStatutaire: null, Note: null, ValeurPointIndiciaire: null,
        AssietteCotisable: null, AssietteImposable: null);

    private static BulletinSnapshot SnapshotDeTest()
    {
        var input = new PayrollInput(
            AgentDeTest(), "2025-06-01",
            new Dictionary<string, decimal>(), new Dictionary<string, decimal>(), new Dictionary<string, string>(),
            Array.Empty<RubriqueCalcul>(), Array.Empty<BaremeValue>(), Array.Empty<ConditionEligibilite>(),
            new Dictionary<string, CritereEligibilite>(), Array.Empty<CotisationCalcul>(),
            ProfilFiscal.Standard, RegleIrg: null,
            Array.Empty<DependanceArete>());
        var bulletin = new Bulletin(
            Lignes: Array.Empty<BulletinLigne>(), TotalGains: new Money(75325m), AssietteCotisable: new Money(75325m),
            AssietteImposable: new Money(68546m), TotalRetenues: new Money(6779m), Irg: new Money(10807m), Net: new Money(57739m),
            Audit: new JournalAudit(Array.Empty<EtapeAudit>()));
        return new BulletinSnapshot(input, bulletin, "2025-06-05T10:00:00.0000000Z");
    }

    private static ConsulterBulletinViewModel BuildViewModel(
        out Mock<IBulletinReadRepository> bulletins, out Mock<IRappelRepository> rappelsRepo, out Mock<IDialogService> dialogs)
    {
        bulletins = new Mock<IBulletinReadRepository>();
        rappelsRepo = new Mock<IRappelRepository>();
        dialogs = new Mock<IDialogService>();

        var agentsRead = new Mock<IAgentReadRepository>();
        agentsRead.Setup(a => a.ListerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<AgentResume>>(Array.Empty<AgentResume>()));

        var consulterBulletin = new ConsulterBulletin(bulletins.Object);
        var listerRappels = new ListerRappels(rappelsRepo.Object);
        return new ConsulterBulletinViewModel(
            consulterBulletin, listerRappels, new Mock<IExporterBulletin>().Object, agentsRead.Object, dialogs.Object);
    }

    [Fact]
    public async Task ConsulterAsync_succes_affiche_le_bulletin()
    {
        var vm = BuildViewModel(out var bulletins, out var rappelsRepo, out var dialogs);
        bulletins.Setup(b => b.ConsulterAsync("A-1", "2025-06-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SnapshotDeTest()));
        rappelsRepo.Setup(r => r.ListerAsync("A-1", "2025-06-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<LigneRappel>>([]));
        vm.AgentId = "A-1";
        vm.DatePaie = "2025-06-01";

        await vm.ConsulterCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.NotNull(vm.Resultat);
        Assert.Contains("57", vm.Resultat);
        Assert.NotNull(vm.Bulletin);
        Assert.True(vm.HasBulletin);
        Assert.Empty(vm.Rappels);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ConsulterAsync_aucun_bulletin_affiche_une_erreur()
    {
        var vm = BuildViewModel(out var bulletins, out _, out var dialogs);
        bulletins.Setup(b => b.ConsulterAsync("A-1", "2025-06-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<BulletinSnapshot>(Error.NotFound("Aucun bulletin validé pour l'agent 'A-1' à la date 2025-06-01.")));
        vm.AgentId = "A-1";
        vm.DatePaie = "2025-06-01";

        await vm.ConsulterCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.Null(vm.Resultat);
        Assert.Null(vm.Bulletin);
        Assert.False(vm.HasBulletin);
        dialogs.Verify(d => d.ShowErrorAsync(It.Is<string>(m => m.Contains("bulletin"))), Times.Once);
    }

    [Fact]
    public async Task ConsulterAsync_succes_avec_rappels_peuple_la_collection_rappels()
    {
        var vm = BuildViewModel(out var bulletins, out var rappelsRepo, out _);
        bulletins.Setup(b => b.ConsulterAsync("A-1", "2025-06-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SnapshotDeTest()));
        rappelsRepo.Setup(r => r.ListerAsync("A-1", "2025-06-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<LigneRappel>>(
                [new LigneRappel("QUALIF", new Money(2000m), new Money(2500m), new Money(500m))]));
        vm.AgentId = "A-1";
        vm.DatePaie = "2025-06-01";

        await vm.ConsulterCommand.ExecuteAsync(null);

        var rappel = Assert.Single(vm.Rappels);
        Assert.Equal("QUALIF", rappel.RubriqueId);
        Assert.Equal(500m, rappel.Delta.Amount);
    }
}
