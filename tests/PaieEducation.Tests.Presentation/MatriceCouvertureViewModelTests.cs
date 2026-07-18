using Moq;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Presentation.Workbench;

namespace PaieEducation.Tests.Presentation;

/// <summary>
/// Tests de <see cref="MatriceCouvertureViewModel"/> (Phase 6, tâche 9) —
/// <see cref="IWorkbenchReadRepository"/> mocké ; <see cref="ListerMatriceCouverture"/>
/// et le ViewModel sont réels.
/// </summary>
public class MatriceCouvertureViewModelTests
{
    private static MatriceCouvertureViewModel BuildViewModel(
        out Mock<IWorkbenchReadRepository> workbench, out Mock<IDialogService> dialogs)
    {
        workbench = new Mock<IWorkbenchReadRepository>();
        dialogs = new Mock<IDialogService>();

        return new MatriceCouvertureViewModel(new ListerMatriceCouverture(workbench.Object), dialogs.Object)
        {
            DatePaie = "2026-01-01",
        };
    }

    [Fact]
    public async Task ChargerAsync_succes_peuple_les_cellules()
    {
        var vm = BuildViewModel(out var workbench, out var dialogs);
        workbench.Setup(w => w.ListerCorpsActifsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<CorpsResume>)[new CorpsResume("IDLS", "Inspection")]);
        workbench.Setup(w => w.ListerRubriquesActivesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RubriqueResume>)[new RubriqueResume("ISSRP_45", "ISSRP 45")]);
        workbench.Setup(w => w.ListerGradesActifsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<(string GradeId, string CorpsId)>)[]);
        workbench.Setup(w => w.ListerConditionsCorpsGradeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ConditionEligibilite>)[]);

        await vm.ChargerCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.Single(vm.Cellules);
        Assert.Equal("IDLS", vm.Cellules[0].CorpsId);
        Assert.Equal("ISSRP_45", vm.Cellules[0].RubriqueId);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChargerAsync_appele_deux_fois_remplace_la_collection_sans_dupliquer()
    {
        var vm = BuildViewModel(out var workbench, out _);
        workbench.Setup(w => w.ListerCorpsActifsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<CorpsResume>)[new CorpsResume("IDLS", "Inspection")]);
        workbench.Setup(w => w.ListerRubriquesActivesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RubriqueResume>)[new RubriqueResume("ISSRP_45", "ISSRP 45")]);
        workbench.Setup(w => w.ListerGradesActifsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<(string GradeId, string CorpsId)>)[]);
        workbench.Setup(w => w.ListerConditionsCorpsGradeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ConditionEligibilite>)[]);

        await vm.ChargerCommand.ExecuteAsync(null);
        await vm.ChargerCommand.ExecuteAsync(null);

        Assert.Single(vm.Cellules);
    }
}
