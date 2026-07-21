using Moq;
using PaieEducation.Application.Agents.UseCases;
using PaieEducation.Application.Referentiels.UseCases;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.ValueObjects;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Presentation.Agents;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Presentation.Navigation;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Presentation;

/// <summary>
/// Tests de <see cref="ListeAgentsViewModel"/> — <see cref="IAgentReadRepository"/>
/// et <see cref="INavigationService"/> mockés (même patron que
/// <c>MatriceCouvertureViewModelTests</c> pour le drill-down).
/// </summary>
public class ListeAgentsViewModelTests
{
    private static readonly AgentResume Benali = new("A-1", "MAT-001", "Benali", "Ahmed");
    private static readonly AgentResume Kaci = new("A-2", "MAT-002", "Kaci", "Fatima");

    private static ListeAgentsViewModel BuildViewModel(
        out Mock<IAgentReadRepository> agentsRead, out Mock<INavigationService> navigation, out Mock<IDialogService> dialogs)
    {
        agentsRead = new Mock<IAgentReadRepository>();
        agentsRead.Setup(a => a.ListerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<AgentResume>>([Benali, Kaci]));
        navigation = new Mock<INavigationService>();
        dialogs = new Mock<IDialogService>();

        return new ListeAgentsViewModel(agentsRead.Object, navigation.Object, dialogs.Object);
    }

    [Fact]
    public async Task Constructeur_charge_la_liste_au_montage()
    {
        var vm = BuildViewModel(out _, out _, out var dialogs);
        await vm.ChargerCommand.ExecuteAsync(null); // le montage a déjà lancé un 1er chargement (fire-and-forget) ; on garantit qu'il est terminé.

        Assert.Equal(2, vm.Agents.Count);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Filtre_sur_matricule_nom_ou_prenom_restreint_la_liste()
    {
        var vm = BuildViewModel(out _, out _, out _);
        await vm.ChargerCommand.ExecuteAsync(null);

        vm.Filtre = "kaci";

        Assert.Single(vm.Agents);
        Assert.Equal("MAT-002", vm.Agents[0].Matricule);
    }

    [Fact]
    public async Task Filtre_vide_restaure_la_liste_complete()
    {
        var vm = BuildViewModel(out _, out _, out _);
        await vm.ChargerCommand.ExecuteAsync(null);

        vm.Filtre = "kaci";
        vm.Filtre = string.Empty;

        Assert.Equal(2, vm.Agents.Count);
    }

    [Fact]
    public async Task Consulter_precharge_la_fiche_agent_avec_l_identifiant_de_la_ligne()
    {
        var vm = BuildViewModel(out _, out var navigation, out _);
        Action<FicheAgentViewModel>? configurateurCapture = null;
        navigation
            .Setup(n => n.OpenTab(It.IsAny<string>(), It.IsAny<Action<FicheAgentViewModel>>()))
            .Callback<string, Action<FicheAgentViewModel>>((_, a) => configurateurCapture = a);

        vm.ConsulterCommand.Execute("A-1");

        navigation.Verify(n => n.OpenTab(It.IsAny<string>(), It.IsAny<Action<FicheAgentViewModel>>()), Times.Once);
        Assert.NotNull(configurateurCapture);

        var fiche = BuildFicheAgentViewModelPourNavigation(out var ficheAgentsRead);
        ficheAgentsRead.Setup(r => r.ObtenirAsync("A-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentDetail(
                "A-1", "MAT-001", "Benali", "Ahmed", "1990-01-01", "2015-09-01", "M", "MARIE", "ACTIF",
                "PEM-G1", "Professeur", "Prof. École", 13, 5, "STATUTAIRE", null, null, null, "2015-09-01", "Recrutement"));

        configurateurCapture!(fiche);
        // Le configurateur a déjà lancé un chargement fire-and-forget ; on le
        // rappelle (idempotent, même AgentId) pour asserter de façon déterministe
        // une fois le chargement terminé — même idiome que
        // Constructeur_charge_la_liste_au_montage ci-dessus.
        await fiche.ChargerCommand.ExecuteAsync(null);

        Assert.Equal("A-1", fiche.AgentId);
        Assert.True(fiche.HasDetail);
    }

    [Fact]
    public async Task ChargerAsync_echec_affiche_une_erreur()
    {
        var agentsRead = new Mock<IAgentReadRepository>();
        agentsRead.Setup(a => a.ListerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<IReadOnlyList<AgentResume>>(Error.Failure("boom")));
        var dialogs = new Mock<IDialogService>();
        var vm = new ListeAgentsViewModel(agentsRead.Object, new Mock<INavigationService>().Object, dialogs.Object);

        await vm.ChargerCommand.ExecuteAsync(null);

        Assert.Empty(vm.Agents);
        dialogs.Verify(d => d.ShowErrorAsync("boom"), Times.AtLeastOnce);
    }

    /// <summary>
    /// Construit un <see cref="FicheAgentViewModel"/> pleinement mocké (les 8
    /// dépendances) pour vérifier un drill-down réel — <see cref="ChargerAsync"/>
    /// charge d'abord tous les référentiels de sélecteurs avant la fiche
    /// elle-même, chaque port doit donc être configuré (même discipline que
    /// <c>MatriceCouvertureViewModelTests.BuildFicheRubriqueViewModelPourNavigation</c>).
    /// </summary>
    private static FicheAgentViewModel BuildFicheAgentViewModelPourNavigation(out Mock<IAgentReadRepository> agentRead)
    {
        agentRead = new Mock<IAgentReadRepository>();
        agentRead.Setup(r => r.ListerSexesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<NomenclatureItem>>([]));
        agentRead.Setup(r => r.ListerSituationsFamilialesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<NomenclatureItem>>([]));
        agentRead.Setup(r => r.ListerTypesContratAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<NomenclatureItem>>([]));

        var referentiels = new Mock<IReferentielReadRepository>();
        referentiels.Setup(r => r.ListerGradesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ReferentielItem>>([]));
        referentiels.Setup(r => r.ListerCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ReferentielItem>>([]));
        referentiels.Setup(r => r.ListerEchelonsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ReferentielItem>>([]));
        referentiels.Setup(r => r.ListerFonctionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ReferentielItem>>([]));
        referentiels.Setup(r => r.ListerEtablissementsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ReferentielItem>>([]));

        var agentCarriere = new Mock<IAgentCarriereRepository>();
        agentCarriere.Setup(a => a.ResoudreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<AgentContext>(Error.NotFound("non résolu (test)")));

        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        var dialogs = new Mock<IDialogService>();

        return new FicheAgentViewModel(
            new ConsulterFicheAgent(agentRead.Object),
            new ModifierAgent(new Mock<IAgentRepository>().Object, agentRead.Object, clock.Object),
            new EnregistrerEvenementCarriere(new Mock<IAgentRepository>().Object, agentRead.Object, clock.Object),
            new DefinirAttributAgent(new Mock<IAgentRepository>().Object, clock.Object),
            new ListerReferentiels(referentiels.Object),
            agentRead.Object,
            agentCarriere.Object,
            dialogs.Object);
    }
}
