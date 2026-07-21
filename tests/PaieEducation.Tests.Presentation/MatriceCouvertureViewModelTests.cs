using Moq;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Presentation.Navigation;
using PaieEducation.Presentation.Workbench;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Presentation;

/// <summary>
/// Tests de <see cref="MatriceCouvertureViewModel"/> (Phase 6, tâche 9 ;
/// pivotée P7, décisions utilisateur du 20/07/2026 : axe Corps × Rubriques,
/// 3 états sans « non applicable ») — <see cref="IWorkbenchReadRepository"/>
/// mocké ; <see cref="ListerMatriceCouverture"/> et le ViewModel sont réels.
/// </summary>
public class MatriceCouvertureViewModelTests
{
    private static MatriceCouvertureViewModel BuildViewModel(
        out Mock<IWorkbenchReadRepository> workbench, out Mock<INavigationService> navigation, out Mock<IDialogService> dialogs)
    {
        workbench = new Mock<IWorkbenchReadRepository>();
        navigation = new Mock<INavigationService>();
        dialogs = new Mock<IDialogService>();

        return new MatriceCouvertureViewModel(new ListerMatriceCouverture(workbench.Object), navigation.Object, dialogs.Object)
        {
            DatePaie = "2026-01-01",
        };
    }

    private static void SetupDeuxCorpsDeuxRubriques(Mock<IWorkbenchReadRepository> workbench)
    {
        workbench.Setup(w => w.ListerCorpsActifsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<CorpsResume>)[new CorpsResume("IDLS", "Inspection"), new CorpsResume("PEM", "Prof. Ens. Fond.")]);
        workbench.Setup(w => w.ListerRubriquesActivesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RubriqueResume>)[new RubriqueResume("ISSRP_45", "ISSRP 45"), new RubriqueResume("IEP", "Indemnité")]);
        workbench.Setup(w => w.ListerGradesActifsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<(string GradeId, string CorpsId)>)[]);
        workbench.Setup(w => w.ListerConditionsCorpsGradeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ConditionEligibilite>)
            [
                ConditionEligibilite.Creer("C1", "ISSRP_45", "CORPS", Operateur.Egal, "PEM", null,
                    PeriodeReglementaire.Creer("2020-01-01", null)),
                ConditionEligibilite.Creer("C2", "IEP", "CORPS", Operateur.Egal, "PEM", null,
                    PeriodeReglementaire.Creer("2020-01-01", "2025-01-01")),
            ]);
    }

    [Fact]
    public async Task ChargerAsync_pivote_une_ligne_par_corps_et_une_colonne_par_rubrique()
    {
        var vm = BuildViewModel(out var workbench, out _, out var dialogs);
        SetupDeuxCorpsDeuxRubriques(workbench);

        await vm.ChargerCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.Equal(2, vm.Lignes.Count);
        Assert.Equal(["IEP", "ISSRP_45"], vm.Colonnes.OrderBy(c => c, StringComparer.Ordinal));
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);

        var pem = Assert.Single(vm.Lignes, l => l.CorpsId == "PEM");
        Assert.Equal(EtatCouverture.Active, pem.EtatsParRubrique["ISSRP_45"]);
        Assert.Equal(EtatCouverture.Inactive, pem.EtatsParRubrique["IEP"]);

        var idls = Assert.Single(vm.Lignes, l => l.CorpsId == "IDLS");
        Assert.Equal(EtatCouverture.NonCouverte, idls.EtatsParRubrique["ISSRP_45"]);
        Assert.Equal(EtatCouverture.NonCouverte, idls.EtatsParRubrique["IEP"]);
    }

    [Fact]
    public async Task ChargerAsync_appele_deux_fois_remplace_le_pivot_sans_dupliquer()
    {
        var vm = BuildViewModel(out var workbench, out _, out _);
        SetupDeuxCorpsDeuxRubriques(workbench);

        await vm.ChargerCommand.ExecuteAsync(null);
        await vm.ChargerCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Lignes.Count);
        Assert.Equal(2, vm.Colonnes.Count);
    }

    [Fact]
    public async Task FiltreCorps_restreint_les_lignes_sans_toucher_aux_colonnes()
    {
        var vm = BuildViewModel(out var workbench, out _, out _);
        SetupDeuxCorpsDeuxRubriques(workbench);
        await vm.ChargerCommand.ExecuteAsync(null);

        vm.FiltreCorps = "pem";

        Assert.Single(vm.Lignes);
        Assert.Equal("PEM", vm.Lignes[0].CorpsId);
        Assert.Equal(2, vm.Colonnes.Count);
    }

    [Fact]
    public async Task FiltreRubrique_restreint_les_colonnes_et_les_etats_des_lignes()
    {
        var vm = BuildViewModel(out var workbench, out _, out _);
        SetupDeuxCorpsDeuxRubriques(workbench);
        await vm.ChargerCommand.ExecuteAsync(null);

        vm.FiltreRubrique = "ISSRP";

        Assert.Equal(["ISSRP_45"], vm.Colonnes);
        Assert.All(vm.Lignes, l => Assert.Single(l.EtatsParRubrique));
    }

    [Fact]
    public async Task FiltreEtat_ne_garde_que_les_corps_ayant_au_moins_une_cellule_dans_cet_etat()
    {
        var vm = BuildViewModel(out var workbench, out _, out _);
        SetupDeuxCorpsDeuxRubriques(workbench);
        await vm.ChargerCommand.ExecuteAsync(null);

        vm.FiltreEtat = "Non couvertes";

        var ligne = Assert.Single(vm.Lignes);
        Assert.Equal("IDLS", ligne.CorpsId);
    }

    [Fact]
    public async Task FiltreEtat_tous_ne_filtre_rien()
    {
        var vm = BuildViewModel(out var workbench, out _, out _);
        SetupDeuxCorpsDeuxRubriques(workbench);
        await vm.ChargerCommand.ExecuteAsync(null);

        vm.FiltreEtat = "Non couvertes";
        vm.FiltreEtat = "Tous";

        Assert.Equal(2, vm.Lignes.Count);
    }

    [Fact]
    public void NaviguerVersFiche_precharge_la_fiche_rubrique_avec_l_identifiant_de_la_colonne()
    {
        var vm = BuildViewModel(out _, out var navigation, out _);
        vm.DatePaie = "2026-03-15";
        Action<FicheRubriqueViewModel>? configurateurCapture = null;
        navigation
            .Setup(n => n.OpenTab(It.IsAny<string>(), It.IsAny<Action<FicheRubriqueViewModel>>()))
            .Callback<string, Action<FicheRubriqueViewModel>>((_, a) => configurateurCapture = a);

        vm.NaviguerVersFicheCommand.Execute("ISSRP_45");

        navigation.Verify(n => n.OpenTab(It.IsAny<string>(), It.IsAny<Action<FicheRubriqueViewModel>>()), Times.Once);
        Assert.NotNull(configurateurCapture);

        var fiche = BuildFicheRubriqueViewModelPourNavigation(out var workbenchFiche);
        workbenchFiche.Setup(w => w.ObtenirRubriqueAsync("ISSRP_45", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RubriqueDetail("ISSRP_45", "ISSRP 45%", "GAIN", "TRAITEMENT", "MENSUELLE", null, 10, true, false, null, true));
        workbenchFiche.Setup(w => w.ListerBaremesRubriqueAsync("ISSRP_45", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<BaremeValue>)[]);
        workbenchFiche.Setup(w => w.ListerConditionsParRubriqueAsync("ISSRP_45", "2026-03-15", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ConditionEligibilite>)[]);
        workbenchFiche.Setup(w => w.ListerGroupesParRubriqueAsync("ISSRP_45", "2026-03-15", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GroupeEligibilite>)[]);

        configurateurCapture!(fiche);

        Assert.Equal("ISSRP_45", fiche.RubriqueId);
        Assert.Equal("2026-03-15", fiche.DatePaie);
    }

    private static FicheRubriqueViewModel BuildFicheRubriqueViewModelPourNavigation(out Mock<IWorkbenchReadRepository> workbench)
    {
        workbench = new Mock<IWorkbenchReadRepository>();
        workbench.Setup(w => w.ListerCriteresParIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<string, CritereEligibilite>)new Dictionary<string, CritereEligibilite>());
        var groupes = new Mock<IGroupeEligibiliteRepository>();
        var regles = new Mock<IRegleEligibiliteRepository>();
        var dialogs = new Mock<IDialogService>();
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
}
