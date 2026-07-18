using Moq;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Workbench.Constants;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Presentation.Workbench;

namespace PaieEducation.Tests.Presentation;

/// <summary>
/// Tests de <see cref="AuditLogViewModel"/> (Phase 6, tâche 4) —
/// <see cref="IAuditLogRepository"/> mocké ; <see cref="ListerAuditLog"/>
/// et le ViewModel sont réels.
/// </summary>
public class AuditLogViewModelTests
{
    private static readonly EntreeAuditLog Entree = new(
        1, "2026-07-17T10:00:00Z", "admin", AuditActions.AppliquerEvolution, AuditEntityTypes.ValeurPoint, "VP-2026-01-01", null, null);

    [Fact]
    public async Task ChargerAsync_succes_peuple_les_entrees()
    {
        var auditLog = new Mock<IAuditLogRepository>();
        auditLog.Setup(a => a.ListerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<EntreeAuditLog>>([Entree]));
        var dialogs = new Mock<IDialogService>();

        var vm = new AuditLogViewModel(new ListerAuditLog(auditLog.Object), dialogs.Object);
        await vm.ChargerCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.Single(vm.Entrees);
        Assert.Equal(AuditActions.AppliquerEvolution, vm.Entrees[0].Action);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChargerAsync_echec_du_repository_affiche_l_erreur()
    {
        var auditLog = new Mock<IAuditLogRepository>();
        auditLog.Setup(a => a.ListerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<IReadOnlyList<EntreeAuditLog>>(Error.Failure("Panne base de données.")));
        var dialogs = new Mock<IDialogService>();

        var vm = new AuditLogViewModel(new ListerAuditLog(auditLog.Object), dialogs.Object);
        await vm.ChargerCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.Empty(vm.Entrees);
        // Times.AtLeastOnce (pas Once) : le constructeur invoque déjà
        // ChargerCommand en fire-and-forget (patron établi, cf. sélecteurs
        // référentiels) — cet appel explicite s'ajoute au premier, non
        // déterministe quant au nombre exact d'invocations en cas d'échec.
        dialogs.Verify(d => d.ShowErrorAsync(It.Is<string>(m => m.Contains("Panne"))), Times.AtLeastOnce);
    }
}
