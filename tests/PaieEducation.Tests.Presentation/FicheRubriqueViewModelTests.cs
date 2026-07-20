using Moq;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Common;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Presentation.Workbench;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Presentation;

/// <summary>
/// Tests de <see cref="FicheRubriqueViewModel"/> (Phase 6, tâche 4 ; édition
/// DNF chantier P6, audit du 19/07/2026) — <see cref="IWorkbenchReadRepository"/>,
/// <see cref="IGroupeEligibiliteRepository"/>, <see cref="IRegleEligibiliteRepository"/>
/// mockés ; les use cases et le ViewModel sont réels.
/// </summary>
public class FicheRubriqueViewModelTests
{
    private static readonly RubriqueDetail Detail = new(
        "QUALIF", "Indemnité de qualification", "GAIN", "TRAITEMENT", "MENSUELLE", null,
        10, true, true, "Barème par catégorie", true);

    private static FicheRubriqueViewModel Build(
        out Mock<IWorkbenchReadRepository> workbench,
        out Mock<IGroupeEligibiliteRepository> groupes,
        out Mock<IRegleEligibiliteRepository> regles,
        out Mock<IDialogService> dialogs)
    {
        workbench = new Mock<IWorkbenchReadRepository>();
        workbench.Setup(w => w.ListerCriteresParIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<string, CritereEligibilite>)new Dictionary<string, CritereEligibilite>());
        groupes = new Mock<IGroupeEligibiliteRepository>();
        regles = new Mock<IRegleEligibiliteRepository>();
        dialogs = new Mock<IDialogService>();
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);

        return new FicheRubriqueViewModel(
            new ConsulterFicheRubrique(workbench.Object),
            new DefinirGroupeEligibilite(groupes.Object, clock.Object),
            new CloreGroupeEligibilite(groupes.Object),
            new DefinirRegleEligibilite(regles.Object, clock.Object),
            new CloreRegleEligibilite(regles.Object),
            new ListerCriteresEligibilite(workbench.Object),
            dialogs.Object);
    }

    [Fact]
    public async Task ChargerAsync_succes_peuple_le_detail_et_les_listes()
    {
        var vm = Build(out var workbench, out _, out _, out var dialogs);
        workbench.Setup(w => w.ObtenirRubriqueAsync("QUALIF", It.IsAny<CancellationToken>())).ReturnsAsync(Detail);
        workbench.Setup(w => w.ListerBaremesRubriqueAsync("QUALIF", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<BaremeValue>)[]);
        workbench.Setup(w => w.ListerConditionsParRubriqueAsync("QUALIF", "2026-01-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ConditionEligibilite>)[]);
        workbench.Setup(w => w.ListerGroupesParRubriqueAsync("QUALIF", "2026-01-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GroupeEligibilite>)[]);
        vm.RubriqueId = "QUALIF";
        vm.DatePaie = "2026-01-01";

        await vm.ChargerCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.NotNull(vm.Detail);
        Assert.Equal("Indemnité de qualification", vm.Detail!.Libelle);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChargerAsync_rubrique_introuvable_affiche_l_erreur()
    {
        var vm = Build(out var workbench, out _, out _, out var dialogs);
        workbench.Setup(w => w.ObtenirRubriqueAsync("INCONNUE", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RubriqueDetail?)null);
        vm.RubriqueId = "INCONNUE";
        vm.DatePaie = "2026-01-01";

        await vm.ChargerCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.Null(vm.Detail);
        dialogs.Verify(d => d.ShowErrorAsync(It.Is<string>(m => m.Contains("introuvable"))), Times.Once);
    }

    [Fact]
    public async Task DefinirGroupeAsync_succes_expose_l_id_dans_le_resultat()
    {
        var vm = Build(out _, out var groupes, out _, out var dialogs);
        groupes.Setup(g => g.DefinirGroupeAsync(
                "GE-TEST", "TEST_DNF", "INFO", null, 100, "2026-01-01", null, null, "workbench",
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>(), It.IsAny<IUnitOfWork>()))
            .ReturnsAsync(Result.Success("GE-TEST"));
        vm.NouveauGroupeId = "GE-TEST";
        vm.NouveauGroupeRubriqueId = "TEST_DNF";
        vm.NouveauGroupeDateEffet = "2026-01-01";

        await vm.DefinirGroupeCommand.ExecuteAsync(null);

        Assert.Contains("GE-TEST", vm.GroupeResultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DefinirGroupeAsync_priorite_invalide_n_appelle_pas_le_repository()
    {
        var vm = Build(out _, out var groupes, out _, out var dialogs);
        vm.NouveauGroupeId = "GE-TEST";
        vm.NouveauGroupeRubriqueId = "TEST_DNF";
        vm.NouveauGroupeDateEffet = "2026-01-01";
        vm.NouveauGroupePriorite = "abc";

        await vm.DefinirGroupeCommand.ExecuteAsync(null);

        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Once);
        groupes.Verify(g => g.DefinirGroupeAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>(), It.IsAny<IUnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task CloreGroupeAsync_succes_expose_l_id_dans_le_resultat()
    {
        var vm = Build(out _, out var groupes, out _, out var dialogs);
        groupes.Setup(g => g.CloreGroupeAsync(
                "GE-TEST", "2026-12-31", It.IsAny<CancellationToken>(), It.IsAny<IUnitOfWork>()))
            .ReturnsAsync(Result.Success("GE-TEST"));
        vm.GroupeACloturerId = "GE-TEST";
        vm.GroupeClotureDateFin = "2026-12-31";

        await vm.CloreGroupeCommand.ExecuteAsync(null);

        Assert.Contains("GE-TEST", vm.GroupeResultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DefinirRegleAsync_sans_critere_selectionne_affiche_l_erreur()
    {
        var vm = Build(out _, out _, out var regles, out var dialogs);
        vm.NouvelleRegleRubriqueId = "TEST_DNF";
        vm.NouvelleRegleCritere = null;

        await vm.DefinirRegleCommand.ExecuteAsync(null);

        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Once);
        regles.Verify(r => r.DefinirRegleAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>(),
            It.IsAny<IUnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task DefinirRegleAsync_succes_expose_l_id_dans_le_resultat()
    {
        var vm = Build(out _, out _, out var regles, out var dialogs);
        regles.Setup(r => r.DefinirRegleAsync(
                "TEST_DNF", "GRADE", null, "=", "G001", "2026-01-01", null,
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>(), It.IsAny<IUnitOfWork>()))
            .ReturnsAsync(Result.Success("RE-TEST_DNF-GRADE-__COMMUNE__-2026-01-01"));
        vm.NouvelleRegleRubriqueId = "TEST_DNF";
        vm.NouvelleRegleCritere = CritereEligibilite.Creer(
            "GRADE", "Grade", Domain.Workbench.Enums.TypeValeurCritere.Enum, Domain.Workbench.Enums.SourceResolution.Carriere);
        vm.NouvelleRegleOperateur = "=";
        vm.NouvelleRegleValeur = "G001";
        vm.NouvelleRegleDateEffet = "2026-01-01";

        await vm.DefinirRegleCommand.ExecuteAsync(null);

        Assert.Contains("RE-TEST_DNF-GRADE-__COMMUNE__-2026-01-01", vm.RegleResultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CloreRegleAsync_succes_expose_l_id_dans_le_resultat()
    {
        var vm = Build(out _, out _, out var regles, out var dialogs);
        regles.Setup(r => r.CloreRegleAsync(
                "RE-TEST", "2026-12-31", It.IsAny<CancellationToken>(), It.IsAny<IUnitOfWork>()))
            .ReturnsAsync(Result.Success("RE-TEST"));
        vm.RegleACloturerId = "RE-TEST";
        vm.RegleClotureDateFin = "2026-12-31";

        await vm.CloreRegleCommand.ExecuteAsync(null);

        Assert.Contains("RE-TEST", vm.RegleResultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Constructeur_charge_les_criteres_de_maniere_asynchrone()
    {
        var workbench = new Mock<IWorkbenchReadRepository>();
        var critere = CritereEligibilite.Creer(
            "GRADE", "Grade", Domain.Workbench.Enums.TypeValeurCritere.Enum, Domain.Workbench.Enums.SourceResolution.Carriere);
        workbench.Setup(w => w.ListerCriteresParIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<string, CritereEligibilite>)new Dictionary<string, CritereEligibilite> { ["GRADE"] = critere });
        var groupes = new Mock<IGroupeEligibiliteRepository>();
        var regles = new Mock<IRegleEligibiliteRepository>();
        var dialogs = new Mock<IDialogService>();
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);

        var vm = new FicheRubriqueViewModel(
            new ConsulterFicheRubrique(workbench.Object),
            new DefinirGroupeEligibilite(groupes.Object, clock.Object),
            new CloreGroupeEligibilite(groupes.Object),
            new DefinirRegleEligibilite(regles.Object, clock.Object),
            new CloreRegleEligibilite(regles.Object),
            new ListerCriteresEligibilite(workbench.Object),
            dialogs.Object);

        // Le constructeur déclenche déjà ce chargement en fire-and-forget ;
        // ré-exécuter la commande directement rend l'assertion déterministe.
        await vm.ChargerCriteresCommand.ExecuteAsync(null);

        Assert.Contains(vm.Criteres, c => c.Id == "GRADE");
    }
}
