using Moq;
using PaieEducation.Application.Agents.UseCases;
using PaieEducation.Application.Referentiels.UseCases;
using PaieEducation.Domain.Agents;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.ValueObjects;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Presentation.Agents;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Presentation;

/// <summary>
/// Tests de <see cref="CreerAgentViewModel"/> (Phase 6, tâche 3) —
/// <see cref="IAgentRepository"/>, <see cref="IAgentReadRepository"/>,
/// <see cref="IReferentielReadRepository"/> et <see cref="IClock"/> mockés ;
/// <see cref="CreerAgent"/>, <see cref="ListerReferentiels"/> et le ViewModel
/// sont réels.
/// </summary>
public class CreerAgentViewModelTests
{
    private static readonly ReferentielItem Grade = new("PDLP-G105", "Professeur");
    private static readonly ReferentielItem Categorie = new("13", "Catégorie 13");
    private static readonly ReferentielItem Echelon = new("5", "Échelon 5");

    private static readonly NomenclatureItem SexeM = new("M", "Masculin");
    private static readonly NomenclatureItem SexeF = new("F", "Féminin");
    private static readonly NomenclatureItem Celibataire = new("CELIBATAIRE", "Célibataire");
    private static readonly NomenclatureItem Marie = new("MARIE", "Marié(e)");
    private static readonly NomenclatureItem Statutaire = new("STATUTAIRE", "Statutaire");
    private static readonly NomenclatureItem Contractuel = new("CONTRACTUEL", "Contractuel");

    private static CreerAgentViewModel BuildViewModel(
        out Mock<IAgentRepository> agents, out Mock<IDialogService> dialogs)
    {
        agents = new Mock<IAgentRepository>();
        var agentRead = new Mock<IAgentReadRepository>();
        agentRead.Setup(r => r.ListerSexesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<NomenclatureItem>>([SexeM, SexeF]));
        agentRead.Setup(r => r.ListerSituationsFamilialesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<NomenclatureItem>>([Celibataire, Marie]));
        agentRead.Setup(r => r.ListerTypesContratAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<NomenclatureItem>>([Statutaire, Contractuel]));

        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        dialogs = new Mock<IDialogService>();

        var referentiels = new Mock<IReferentielReadRepository>();
        referentiels.Setup(r => r.ListerGradesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ReferentielItem>>([Grade]));
        referentiels.Setup(r => r.ListerCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ReferentielItem>>([Categorie]));
        referentiels.Setup(r => r.ListerEchelonsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ReferentielItem>>([Echelon]));

        var creerAgent = new CreerAgent(agents.Object, agentRead.Object, clock.Object);
        var listerReferentiels = new ListerReferentiels(referentiels.Object);
        return new CreerAgentViewModel(creerAgent, listerReferentiels, agentRead.Object, dialogs.Object)
        {
            Matricule = "MAT-001",
            Nom = "Test",
            Prenom = "Agent",
            DateNaissance = "1990-01-01",
            DateRecrutement = "2015-09-01",
            GradeSelectionne = Grade,
            CategorieSelectionnee = Categorie,
            EchelonSelectionne = Echelon,
        };
    }

    [Fact]
    public async Task ChargerReferentielsAsync_peuple_les_6_listes()
    {
        var vm = BuildViewModel(out _, out var dialogs);

        await vm.ChargerReferentielsCommand.ExecuteAsync(null);

        Assert.Contains(Grade, vm.GradesDisponibles);
        Assert.Contains(Categorie, vm.CategoriesDisponibles);
        Assert.Contains(Echelon, vm.EchelonsDisponibles);
        Assert.Contains("M", vm.SexesDisponibles);
        Assert.Contains("F", vm.SexesDisponibles);
        Assert.Contains("CELIBATAIRE", vm.SituationsDisponibles);
        Assert.Contains("STATUTAIRE", vm.TypesContratDisponibles);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreerAsync_succes_renseigne_le_resultat()
    {
        var vm = BuildViewModel(out var agents, out var dialogs);
        agents.Setup(a => a.CreerAsync(
                It.Is<NouvelAgent>(n => n.GradeId == "PDLP-G105" && n.CategorieId == "13" && n.EchelonId == "5"),
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success("A-NOUVEAU"));

        await vm.CreerCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.NotNull(vm.Resultat);
        Assert.Contains("A-NOUVEAU", vm.Resultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreerAsync_matricule_deja_utilise_affiche_une_erreur()
    {
        var vm = BuildViewModel(out var agents, out var dialogs);
        agents.Setup(a => a.CreerAsync(It.IsAny<NouvelAgent>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(Error.Conflict("Le matricule 'MAT-001' est déjà utilisé.")));

        await vm.CreerCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.Null(vm.Resultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.Is<string>(m => m.Contains("matricule"))), Times.Once);
    }
}
