using Moq;
using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Domain.Calcul.Audit;
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
            ProfilFiscal.Standard, RegleIrg: null);
        var bulletin = new Bulletin(
            Lignes: Array.Empty<BulletinLigne>(), TotalGains: 75325m, AssietteCotisable: 75325m,
            AssietteImposable: 68546m, TotalRetenues: 6779m, Irg: 10807m, Net: 57739m,
            Audit: new JournalAudit(Array.Empty<EtapeAudit>()));
        return new BulletinSnapshot(input, bulletin, "2025-06-05T10:00:00.0000000Z");
    }

    [Fact]
    public async Task ConsulterAsync_succes_affiche_le_bulletin()
    {
        var bulletins = new Mock<IBulletinReadRepository>();
        bulletins.Setup(b => b.ConsulterAsync("A-1", "2025-06-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SnapshotDeTest()));

        var consulterBulletin = new ConsulterBulletin(bulletins.Object);
        var dialogs = new Mock<IDialogService>();
        var vm = new ConsulterBulletinViewModel(consulterBulletin, dialogs.Object)
        {
            AgentId = "A-1",
            DatePaie = "2025-06-01",
        };

        await vm.ConsulterCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.NotNull(vm.Resultat);
        Assert.Contains("57", vm.Resultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ConsulterAsync_aucun_bulletin_affiche_une_erreur()
    {
        var bulletins = new Mock<IBulletinReadRepository>();
        bulletins.Setup(b => b.ConsulterAsync("A-1", "2025-06-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<BulletinSnapshot>(Error.NotFound("Aucun bulletin validé pour l'agent 'A-1' à la date 2025-06-01.")));

        var consulterBulletin = new ConsulterBulletin(bulletins.Object);
        var dialogs = new Mock<IDialogService>();
        var vm = new ConsulterBulletinViewModel(consulterBulletin, dialogs.Object)
        {
            AgentId = "A-1",
            DatePaie = "2025-06-01",
        };

        await vm.ConsulterCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.Null(vm.Resultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.Is<string>(m => m.Contains("bulletin"))), Times.Once);
    }
}
