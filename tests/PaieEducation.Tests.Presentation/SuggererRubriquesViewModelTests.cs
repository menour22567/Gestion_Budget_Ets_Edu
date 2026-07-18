using Moq;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Presentation.Workbench;
using PaieEducation.Shared.Time;
using SuggererRubriquesUseCase = PaieEducation.Application.Workbench.UseCases.SuggererRubriques;

namespace PaieEducation.Tests.Presentation;

/// <summary>
/// Tests de <see cref="SuggererRubriquesViewModel"/> (Phase 6, tâche 3) —
/// <see cref="IAgentCarriereRepository"/>, <see cref="IWorkbenchReadRepository"/>
/// et <see cref="IAgentRubriqueRepository"/> sont mockés (ce dernier partagé
/// par les 5 use cases construits, comme en production via DI) ; les use
/// cases et le ViewModel sont réels.
/// </summary>
public class SuggererRubriquesViewModelTests
{
    private static AgentContext AgentDeTest() => new(
        Filiere: null, Corps: null, Grade: null, Categorie: null, Echelon: null,
        AncienneteAnnees: null, Fonction: null, TypeContrat: null, TypeEtablissement: null,
        OrigineStatutaire: null, Note: null, ValeurPointIndiciaire: null,
        AssietteCotisable: null, AssietteImposable: null);

    private static SuggererRubriquesViewModel BuildViewModel(
        Mock<IAgentCarriereRepository> agents, Mock<IWorkbenchReadRepository> workbench,
        Mock<IAgentRubriqueRepository> agentRubriques, out Mock<IDialogService> dialogs)
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        dialogs = new Mock<IDialogService>();

        var suggerer = new SuggererRubriquesUseCase(agents.Object, workbench.Object, agentRubriques.Object, clock.Object);
        var lister = new ListerAffectationsAgent(agentRubriques.Object);
        var accepter = new AccepterSuggestion(agentRubriques.Object, clock.Object);
        var supprimer = new SupprimerAffectation(agentRubriques.Object, clock.Object);
        var suspendre = new SuspendreAffectation(agentRubriques.Object, clock.Object);

        return new SuggererRubriquesViewModel(suggerer, lister, accepter, supprimer, suspendre, dialogs.Object);
    }

    [Fact]
    public async Task SuggererAsync_aucune_rubrique_affectable_affiche_un_resultat_vide_et_recharge_la_liste_vide()
    {
        var agents = new Mock<IAgentCarriereRepository>();
        agents.Setup(a => a.ResoudreAsync("A-1", "2025-06-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(AgentDeTest()));

        var workbench = new Mock<IWorkbenchReadRepository>();
        workbench.Setup(w => w.ListerRubriquesAffectablesAsync("2025-06-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        var agentRubriques = new Mock<IAgentRubriqueRepository>();
        agentRubriques.Setup(r => r.ListerParAgentAsync("A-1", "2025-06-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<AffectationRubrique>>(Array.Empty<AffectationRubrique>()));

        var vm = BuildViewModel(agents, workbench, agentRubriques, out var dialogs);
        vm.AgentId = "A-1";
        vm.DatePaie = "2025-06-01";

        await vm.SuggererCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.Equal("Aucune rubrique suggérée.", vm.Resultat);
        Assert.Empty(vm.Affectations);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SuggererAsync_agent_introuvable_affiche_une_erreur()
    {
        var agents = new Mock<IAgentCarriereRepository>();
        agents.Setup(a => a.ResoudreAsync("A-INEXISTANT", "2025-06-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<AgentContext>(Error.NotFound("Agent introuvable : 'A-INEXISTANT'.")));

        var vm = BuildViewModel(
            agents, new Mock<IWorkbenchReadRepository>(), new Mock<IAgentRubriqueRepository>(), out var dialogs);
        vm.AgentId = "A-INEXISTANT";
        vm.DatePaie = "2025-06-01";

        await vm.SuggererCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.Null(vm.Resultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.Is<string>(m => m.Contains("introuvable"))), Times.Once);
    }

    [Fact]
    public async Task AccepterAsync_succes_recharge_les_affectations_avec_le_nouveau_statut()
    {
        var agentRubriques = new Mock<IAgentRubriqueRepository>();
        agentRubriques.Setup(r => r.ChangerStatutAsync("AR-1", "ACCEPTEE", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success("AR-1"));
        agentRubriques.Setup(r => r.ListerParAgentAsync("A-1", "2025-06-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<AffectationRubrique>>(
                [new AffectationRubrique("AR-1", "ISSRP_45", 1, "ACCEPTEE", "GROUPE:GE-ISSRP45-ORIGINE@2025-01-01", "2025-06-01", null)]));

        var vm = BuildViewModel(
            new Mock<IAgentCarriereRepository>(), new Mock<IWorkbenchReadRepository>(), agentRubriques, out var dialogs);
        vm.AgentId = "A-1";
        vm.DatePaie = "2025-06-01";

        await vm.AccepterCommand.ExecuteAsync("AR-1");

        var affectation = Assert.Single(vm.Affectations);
        Assert.Equal("ACCEPTEE", affectation.Statut);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SupprimerAsync_ligne_deja_terminale_affiche_une_erreur()
    {
        var agentRubriques = new Mock<IAgentRubriqueRepository>();
        agentRubriques.Setup(r => r.ChangerStatutAsync("AR-1", "SUPPRIMEE", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(Error.Conflict("L'affectation 'AR-1' est à l'état terminal SUPPRIMEE.")));

        var vm = BuildViewModel(
            new Mock<IAgentCarriereRepository>(), new Mock<IWorkbenchReadRepository>(), agentRubriques, out var dialogs);

        await vm.SupprimerCommand.ExecuteAsync("AR-1");

        Assert.Empty(vm.Affectations);
        dialogs.Verify(d => d.ShowErrorAsync(It.Is<string>(m => m.Contains("terminal"))), Times.Once);
    }
}
