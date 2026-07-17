using Moq;
using PaieEducation.Application.Agents.UseCases;
using PaieEducation.Application.Referentiels.UseCases;
using PaieEducation.Domain.Agents;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.ValueObjects;
using PaieEducation.Domain.Common;
using PaieEducation.Presentation.Agents;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Presentation;

/// <summary>
/// Tests de <see cref="CreerAgentViewModel"/> (Phase 6, tâche 3) —
/// <see cref="IAgentRepository"/>, <see cref="IReferentielReadRepository"/>
/// et <see cref="IClock"/> mockés ; <see cref="CreerAgent"/>,
/// <see cref="ListerReferentiels"/> et le ViewModel sont réels.
/// </summary>
public class CreerAgentViewModelTests
{
    private static readonly ReferentielItem Grade = new("PDLP-G105", "Professeur");
    private static readonly ReferentielItem Categorie = new("13", "Catégorie 13");
    private static readonly ReferentielItem Echelon = new("5", "Échelon 5");

    private static CreerAgentViewModel BuildViewModel(
        out Mock<IAgentRepository> agents, out Mock<IDialogService> dialogs)
    {
        agents = new Mock<IAgentRepository>();
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

        var creerAgent = new CreerAgent(agents.Object, clock.Object);
        var listerReferentiels = new ListerReferentiels(referentiels.Object);
        return new CreerAgentViewModel(creerAgent, listerReferentiels, dialogs.Object)
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
    public async Task ChargerReferentielsAsync_peuple_les_3_listes()
    {
        var vm = BuildViewModel(out _, out var dialogs);

        await vm.ChargerReferentielsCommand.ExecuteAsync(null);

        Assert.Contains(Grade, vm.GradesDisponibles);
        Assert.Contains(Categorie, vm.CategoriesDisponibles);
        Assert.Contains(Echelon, vm.EchelonsDisponibles);
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
