using Moq;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Presentation.Workbench;

namespace PaieEducation.Tests.Presentation;

/// <summary>
/// Tests de <see cref="FicheRubriqueViewModel"/> (Phase 6, tâche 4) —
/// <see cref="IWorkbenchReadRepository"/> mocké ; <see cref="ConsulterFicheRubrique"/>
/// et le ViewModel sont réels.
/// </summary>
public class FicheRubriqueViewModelTests
{
    private static readonly RubriqueDetail Detail = new(
        "QUALIF", "Indemnité de qualification", "GAIN", "TRAITEMENT", "MENSUELLE", null,
        10, true, true, "Barème par catégorie", true);

    [Fact]
    public async Task ChargerAsync_succes_peuple_le_detail_et_les_listes()
    {
        var workbench = new Mock<IWorkbenchReadRepository>();
        workbench.Setup(w => w.ObtenirRubriqueAsync("QUALIF", It.IsAny<CancellationToken>())).ReturnsAsync(Detail);
        workbench.Setup(w => w.ListerBaremesRubriqueAsync("QUALIF", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<BaremeValue>)[]);
        workbench.Setup(w => w.ListerConditionsParRubriqueAsync("QUALIF", "2026-01-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ConditionEligibilite>)[]);
        workbench.Setup(w => w.ListerGroupesParRubriqueAsync("QUALIF", "2026-01-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GroupeEligibilite>)[]);
        var dialogs = new Mock<IDialogService>();

        var vm = new FicheRubriqueViewModel(new ConsulterFicheRubrique(workbench.Object), dialogs.Object)
        {
            RubriqueId = "QUALIF",
            DatePaie = "2026-01-01",
        };

        await vm.ChargerCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.NotNull(vm.Detail);
        Assert.Equal("Indemnité de qualification", vm.Detail!.Libelle);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChargerAsync_rubrique_introuvable_affiche_l_erreur()
    {
        var workbench = new Mock<IWorkbenchReadRepository>();
        workbench.Setup(w => w.ObtenirRubriqueAsync("INCONNUE", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RubriqueDetail?)null);
        var dialogs = new Mock<IDialogService>();

        var vm = new FicheRubriqueViewModel(new ConsulterFicheRubrique(workbench.Object), dialogs.Object)
        {
            RubriqueId = "INCONNUE",
            DatePaie = "2026-01-01",
        };

        await vm.ChargerCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.Null(vm.Detail);
        dialogs.Verify(d => d.ShowErrorAsync(It.Is<string>(m => m.Contains("introuvable"))), Times.Once);
    }
}
