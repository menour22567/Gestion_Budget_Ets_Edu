using Moq;
using PaieEducation.Application.Agents.UseCases;
using PaieEducation.Application.Referentiels.UseCases;
using PaieEducation.Domain.Agents;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.ValueObjects;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Presentation.Agents;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Presentation;

/// <summary>
/// Tests de <see cref="FicheAgentViewModel"/> — <see cref="IAgentRepository"/>,
/// <see cref="IAgentReadRepository"/>, <see cref="IReferentielReadRepository"/>
/// et <see cref="IAgentCarriereRepository"/> mockés ; les 4 use cases et le
/// ViewModel sont réels.
/// </summary>
public class FicheAgentViewModelTests
{
    private static readonly AgentDetail Detail = new(
        "A-1", "MAT-001", "Benali", "Ahmed", "1990-01-01", "2015-09-01", "M", "MARIE", "ACTIF",
        "PEM-G1", "Professeur École primaire", "Prof. École", 13, 5, "STATUTAIRE",
        "Directeur", "Lycée test", "LYCEE", "2015-09-01", "Recrutement", "FONC-1", "ETB-1");

    private static readonly ReferentielItem Grade = new("PEM-G1", "Professeur École primaire");
    private static readonly ReferentielItem Categorie13 = new("13", "Catégorie 13");
    private static readonly ReferentielItem Echelon5 = new("5", "Échelon 5");
    private static readonly ReferentielItem Fonction = new("FONC-1", "Directeur");
    private static readonly ReferentielItem Etablissement = new("ETB-1", "Lycée test");

    private static FicheAgentViewModel BuildViewModel(
        out Mock<IAgentRepository> agents, out Mock<IAgentReadRepository> agentRead,
        out Mock<IAgentCarriereRepository> agentCarriere, out Mock<IDialogService> dialogs)
    {
        agents = new Mock<IAgentRepository>();
        agentRead = new Mock<IAgentReadRepository>();
        agentRead.Setup(r => r.ObtenirAsync("A-1", It.IsAny<CancellationToken>())).ReturnsAsync(Detail);
        agentRead.Setup(r => r.ListerSexesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<NomenclatureItem>>([new("M", "Masculin"), new("F", "Féminin")]));
        agentRead.Setup(r => r.ListerSituationsFamilialesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<NomenclatureItem>>([new("CELIBATAIRE", "Célibataire"), new("MARIE", "Marié(e)")]));
        agentRead.Setup(r => r.ListerTypesContratAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<NomenclatureItem>>([new("STATUTAIRE", "Statutaire"), new("CONTRACTUEL", "Contractuel")]));

        var referentiels = new Mock<IReferentielReadRepository>();
        referentiels.Setup(r => r.ListerGradesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ReferentielItem>>([Grade]));
        referentiels.Setup(r => r.ListerCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ReferentielItem>>([Categorie13]));
        referentiels.Setup(r => r.ListerEchelonsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ReferentielItem>>([Echelon5]));
        referentiels.Setup(r => r.ListerFonctionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ReferentielItem>>([Fonction]));
        referentiels.Setup(r => r.ListerEtablissementsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ReferentielItem>>([Etablissement]));

        agentCarriere = new Mock<IAgentCarriereRepository>();
        agentCarriere.Setup(a => a.ResoudreAsync("A-1", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new AgentContext(
                Filiere: "ENSEIGNANT", Corps: "PEM", Grade: "PEM-G1", Categorie: 13, Echelon: 5,
                AncienneteAnnees: 10, Fonction: "Directeur", TypeContrat: "STATUTAIRE", TypeEtablissement: "LYCEE",
                OrigineStatutaire: "ENSEIGNANT", Note: 15m, ValeurPointIndiciaire: null,
                AssietteCotisable: null, AssietteImposable: null, AnciennetePriveeAnnees: 3)));

        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        dialogs = new Mock<IDialogService>();

        return new FicheAgentViewModel(
            new ConsulterFicheAgent(agentRead.Object),
            new ModifierAgent(agents.Object, agentRead.Object, clock.Object),
            new EnregistrerEvenementCarriere(agents.Object, agentRead.Object, clock.Object),
            new DefinirAttributAgent(agents.Object, clock.Object),
            new ListerReferentiels(referentiels.Object),
            agentRead.Object,
            agentCarriere.Object,
            dialogs.Object);
    }

    [Fact]
    public async Task ChargerAsync_succes_renseigne_le_detail()
    {
        var vm = BuildViewModel(out _, out _, out _, out var dialogs);
        vm.AgentId = "A-1";

        await vm.ChargerCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.True(vm.HasDetail);
        Assert.Equal("Benali Ahmed", vm.Detail!.NomComplet);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChargerAsync_agent_introuvable_affiche_une_erreur_et_ne_renseigne_rien()
    {
        var agentRead = new Mock<IAgentReadRepository>();
        agentRead.Setup(r => r.ObtenirAsync("A-INEXISTANT", It.IsAny<CancellationToken>())).ReturnsAsync((AgentDetail?)null);
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
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        var dialogs = new Mock<IDialogService>();
        var agents = new Mock<IAgentRepository>();

        var vm = new FicheAgentViewModel(
            new ConsulterFicheAgent(agentRead.Object),
            new ModifierAgent(agents.Object, agentRead.Object, clock.Object),
            new EnregistrerEvenementCarriere(agents.Object, agentRead.Object, clock.Object),
            new DefinirAttributAgent(agents.Object, clock.Object),
            new ListerReferentiels(referentiels.Object),
            agentRead.Object,
            new Mock<IAgentCarriereRepository>().Object,
            dialogs.Object)
        {
            AgentId = "A-INEXISTANT",
        };

        await vm.ChargerCommand.ExecuteAsync(null);

        Assert.False(vm.EnCours);
        Assert.False(vm.HasDetail);
        Assert.Null(vm.Detail);
        dialogs.Verify(d => d.ShowErrorAsync(It.Is<string>(m => m.Contains("introuvable"))), Times.Once);
    }

    [Fact]
    public async Task ChargerAsync_identifiant_vide_ne_fait_rien()
    {
        var vm = BuildViewModel(out _, out var agentRead, out _, out _);

        await vm.ChargerCommand.ExecuteAsync(null);

        agentRead.Verify(r => r.ObtenirAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(vm.HasDetail);
    }

    [Fact]
    public async Task ChargerAsync_succes_pre_remplit_le_formulaire_d_identite()
    {
        var vm = BuildViewModel(out _, out _, out _, out _);
        vm.AgentId = "A-1";

        await vm.ChargerCommand.ExecuteAsync(null);

        Assert.Equal("Benali", vm.ModifierNom);
        Assert.Equal("Ahmed", vm.ModifierPrenom);
        Assert.Equal("1990-01-01", vm.ModifierDateNaissance);
        Assert.Equal("M", vm.ModifierSexe);
        Assert.Equal("MARIE", vm.ModifierSituationFamiliale);
        Assert.Equal("ACTIF", vm.ModifierStatut);
    }

    [Fact]
    public async Task ChargerAsync_succes_pre_selectionne_grade_categorie_echelon_de_la_carriere_en_vigueur()
    {
        var vm = BuildViewModel(out _, out _, out _, out _);
        vm.AgentId = "A-1";

        await vm.ChargerCommand.ExecuteAsync(null);

        Assert.Equal(Grade, vm.NouvelleCarriereGrade);
        Assert.Equal(Categorie13, vm.NouvelleCarriereCategorie);
        Assert.Equal(Echelon5, vm.NouvelleCarriereEchelon);
        Assert.Equal(Fonction, vm.NouvelleCarriereFonction);
        Assert.Equal(Etablissement, vm.NouvelleCarriereEtablissement);
        Assert.Equal("STATUTAIRE", vm.NouvelleCarriereTypeContrat);
    }

    [Fact]
    public async Task ChargerAsync_succes_charge_les_attributs_actuellement_en_vigueur()
    {
        var vm = BuildViewModel(out _, out _, out _, out _);
        vm.AgentId = "A-1";

        await vm.ChargerCommand.ExecuteAsync(null);

        Assert.Equal(15m, vm.AttributNoteActuelle);
        Assert.Equal("ENSEIGNANT", vm.AttributOrigineActuelle);
        Assert.Equal(3, vm.AttributAnciennetePriveeActuelle);
    }

    [Fact]
    public async Task ModifierIdentiteAsync_succes_affiche_un_resultat()
    {
        var vm = BuildViewModel(out var agents, out _, out _, out var dialogs);
        vm.AgentId = "A-1";
        await vm.ChargerCommand.ExecuteAsync(null);
        agents.Setup(a => a.ModifierAsync(It.IsAny<AgentModifie>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success("A-1"));

        await vm.ModifierIdentiteCommand.ExecuteAsync(null);

        Assert.False(vm.IdentiteEnCours);
        Assert.NotNull(vm.IdentiteResultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ModifierIdentiteAsync_echec_affiche_une_erreur()
    {
        var vm = BuildViewModel(out var agents, out _, out _, out var dialogs);
        vm.AgentId = "A-1";
        await vm.ChargerCommand.ExecuteAsync(null);
        agents.Setup(a => a.ModifierAsync(It.IsAny<AgentModifie>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(Error.Validation("Sexe invalide.")));

        await vm.ModifierIdentiteCommand.ExecuteAsync(null);

        Assert.Null(vm.IdentiteResultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.Is<string>(m => m.Contains("Sexe"))), Times.Once);
    }

    [Fact]
    public async Task EnregistrerCarriereAsync_sans_grade_categorie_ou_echelon_affiche_une_erreur_sans_appeler_le_repository()
    {
        var vm = BuildViewModel(out var agents, out _, out _, out var dialogs);
        vm.AgentId = "A-1";
        await vm.ChargerCommand.ExecuteAsync(null);
        vm.NouvelleCarriereGrade = null; // annule le pré-remplissage

        await vm.EnregistrerCarriereCommand.ExecuteAsync(null);

        agents.Verify(a => a.EnregistrerEvenementCarriereAsync(
            It.IsAny<EvenementCarriere>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        dialogs.Verify(d => d.ShowErrorAsync(It.Is<string>(m => m.Contains("requis"))), Times.Once);
    }

    [Fact]
    public async Task EnregistrerCarriereAsync_succes_affiche_un_resultat()
    {
        var vm = BuildViewModel(out var agents, out _, out _, out var dialogs);
        vm.AgentId = "A-1";
        await vm.ChargerCommand.ExecuteAsync(null);
        vm.NouvelleCarriereDateEffet = "2026-01-01";
        vm.NouvelleCarriereMotif = "Avancement d'échelon";
        agents.Setup(a => a.EnregistrerEvenementCarriereAsync(
                It.IsAny<EvenementCarriere>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success("C-NOUVEAU"));

        await vm.EnregistrerCarriereCommand.ExecuteAsync(null);

        Assert.False(vm.CarriereEnCours);
        Assert.Contains("C-NOUVEAU", vm.CarriereResultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task EnregistrerCarriereAsync_echec_affiche_une_erreur()
    {
        var vm = BuildViewModel(out var agents, out _, out _, out var dialogs);
        vm.AgentId = "A-1";
        await vm.ChargerCommand.ExecuteAsync(null);
        vm.NouvelleCarriereDateEffet = "2010-01-01"; // antérieure à la carrière en vigueur
        vm.NouvelleCarriereMotif = "Test";
        agents.Setup(a => a.EnregistrerEvenementCarriereAsync(
                It.IsAny<EvenementCarriere>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(Error.Validation("La nouvelle date d'effet doit être postérieure.")));

        await vm.EnregistrerCarriereCommand.ExecuteAsync(null);

        Assert.Null(vm.CarriereResultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.Is<string>(m => m.Contains("postérieure"))), Times.Once);
    }

    [Fact]
    public async Task DefinirAttributAsync_succes_affiche_un_resultat()
    {
        var vm = BuildViewModel(out var agents, out _, out _, out var dialogs);
        vm.AgentId = "A-1";
        await vm.ChargerCommand.ExecuteAsync(null);
        vm.AttributValeur = "16";
        vm.AttributDateEffet = "2026-01-01";
        agents.Setup(a => a.DefinirAttributAsync(
                "A-1", It.IsAny<string>(), "16", "2026-01-01", null, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success("ATTR-NOUVEAU"));

        await vm.DefinirAttributCommand.ExecuteAsync(null);

        Assert.False(vm.AttributEnCours);
        Assert.Contains("ATTR-NOUVEAU", vm.AttributResultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DefinirAttributAsync_echec_affiche_une_erreur()
    {
        var vm = BuildViewModel(out var agents, out _, out _, out var dialogs);
        vm.AgentId = "A-1";
        await vm.ChargerCommand.ExecuteAsync(null);
        agents.Setup(a => a.DefinirAttributAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(Error.Conflict("Un attribut existe déjà à cette date.")));

        await vm.DefinirAttributCommand.ExecuteAsync(null);

        Assert.Null(vm.AttributResultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.Is<string>(m => m.Contains("existe déjà"))), Times.Once);
    }
}
