using Moq;
using PaieEducation.Application.Referentiels.UseCases;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.ValueObjects;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Presentation.Referentiels;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Presentation;

/// <summary>
/// Tests de <see cref="GrilleIndiciaireViewModel"/> (Phase 6, tâche 3) —
/// <see cref="IGrilleIndiciaireRepository"/> et <see cref="IReferentielReadRepository"/>
/// mockés ; les 5 use cases et le ViewModel sont réels.
/// </summary>
public class GrilleIndiciaireViewModelTests
{
    private static readonly ReferentielItem Categorie = new("13", "Catégorie 13");
    private static readonly ReferentielItem Echelon = new("5", "Échelon 5");

    private static GrilleIndiciaireViewModel BuildViewModel(
        out Mock<IGrilleIndiciaireRepository> grille, out Mock<IDialogService> dialogs)
    {
        grille = new Mock<IGrilleIndiciaireRepository>();
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        dialogs = new Mock<IDialogService>();

        var referentiels = new Mock<IReferentielReadRepository>();
        referentiels.Setup(r => r.ListerGradesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ReferentielItem>>([]));
        referentiels.Setup(r => r.ListerCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ReferentielItem>>([Categorie]));
        referentiels.Setup(r => r.ListerEchelonsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ReferentielItem>>([Echelon]));

        return new GrilleIndiciaireViewModel(
            new DefinirValeurPoint(grille.Object, clock.Object),
            new DefinirIndiceMinGrille(grille.Object, clock.Object),
            new DefinirIndiceEchelon(grille.Object, clock.Object),
            new DupliquerVersion(grille.Object, clock.Object),
            new ListerReferentiels(referentiels.Object),
            dialogs.Object);
    }

    [Fact]
    public async Task ChargerReferentielsAsync_peuple_categories_et_echelons()
    {
        var vm = BuildViewModel(out _, out var dialogs);

        await vm.ChargerReferentielsCommand.ExecuteAsync(null);

        Assert.Contains(Categorie, vm.CategoriesDisponibles);
        Assert.Contains(Echelon, vm.EchelonsDisponibles);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DefinirValeurPointAsync_succes_renseigne_le_resultat()
    {
        var vm = BuildViewModel(out var grille, out var dialogs);
        grille.Setup(g => g.DefinirValeurPointAsync(
                45m, "2026-01-01", "2026", null, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success("VP-2026-01-01"));

        vm.VpValeur = "45";
        vm.VpDateEffet = "2026-01-01";
        vm.VpVersion = "2026";

        await vm.DefinirValeurPointCommand.ExecuteAsync(null);

        Assert.False(vm.VpEnCours);
        Assert.NotNull(vm.VpResultat);
        Assert.Contains("VP-2026-01-01", vm.VpResultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DefinirValeurPointAsync_valeur_non_numerique_affiche_une_erreur_sans_appeler_le_repository()
    {
        var vm = BuildViewModel(out var grille, out var dialogs);
        vm.VpValeur = "pas-un-nombre";
        vm.VpDateEffet = "2026-01-01";
        vm.VpVersion = "2026";

        await vm.DefinirValeurPointCommand.ExecuteAsync(null);

        Assert.Null(vm.VpResultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Once);
        grille.Verify(g => g.DefinirValeurPointAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DefinirIndiceMinAsync_succes_renseigne_le_resultat()
    {
        var vm = BuildViewModel(out var grille, out var dialogs);
        grille.Setup(g => g.DefinirIndiceMinAsync(
                "13", 578, "2020-01-01", "v", null, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success("GI-13-2020-01-01"));

        vm.ImCategorieSelectionnee = Categorie;
        vm.ImIndiceMin = "578";
        vm.ImDateEffet = "2020-01-01";
        vm.ImVersion = "v";

        await vm.DefinirIndiceMinCommand.ExecuteAsync(null);

        Assert.False(vm.ImEnCours);
        Assert.NotNull(vm.ImResultat);
        Assert.Contains("GI-13-2020-01-01", vm.ImResultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DefinirIndiceEchelonAsync_echec_du_repository_affiche_l_erreur()
    {
        var vm = BuildViewModel(out var grille, out var dialogs);
        grille.Setup(g => g.DefinirIndiceEchelonAsync(
                "5", 100, "2020-01-01", "v", null, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(Error.Conflict("La date d'effet 2020-01-01 existe déjà pour l'échelon '5'.")));

        vm.IeEchelonSelectionne = Echelon;
        vm.IeIndice = "100";
        vm.IeDateEffet = "2020-01-01";
        vm.IeVersion = "v";

        await vm.DefinirIndiceEchelonCommand.ExecuteAsync(null);

        Assert.False(vm.IeEnCours);
        Assert.Null(vm.IeResultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.Is<string>(m => m.Contains("existe déjà"))), Times.Once);
    }

    [Fact]
    public async Task DupliquerValeurPointAsync_succes_renseigne_le_resultat()
    {
        var vm = BuildViewModel(out var grille, out var dialogs);
        grille.Setup(g => g.DupliquerValeurPointAsync(
                "2026-01-01", "2026", "Décret X", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success("VP-2026-01-01"));

        vm.DvDateEffet = "2026-01-01";
        vm.DvVersion = "2026";
        vm.DvSource = "Décret X";

        await vm.DupliquerValeurPointCommand.ExecuteAsync(null);

        Assert.False(vm.DvEnCours);
        Assert.NotNull(vm.DvResultat);
        Assert.Contains("VP-2026-01-01", vm.DvResultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DupliquerValeurPointAsync_echec_du_repository_affiche_l_erreur()
    {
        var vm = BuildViewModel(out var grille, out var dialogs);
        grille.Setup(g => g.DupliquerValeurPointAsync(
                "2026-01-01", "2026", null, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(Error.NotFound("Aucune valeur du point indiciaire en vigueur à dupliquer.")));

        vm.DvDateEffet = "2026-01-01";
        vm.DvVersion = "2026";

        await vm.DupliquerValeurPointCommand.ExecuteAsync(null);

        Assert.False(vm.DvEnCours);
        Assert.Null(vm.DvResultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.Is<string>(m => m.Contains("Aucune valeur"))), Times.Once);
    }
}
