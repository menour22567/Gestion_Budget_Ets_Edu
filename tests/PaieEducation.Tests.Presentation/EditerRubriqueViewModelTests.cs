using Moq;
using PaieEducation.Application.Referentiels.UseCases;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Presentation.Workbench;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Presentation;

/// <summary>
/// Tests de <see cref="EditerRubriqueViewModel"/> (chantier C4.1 — écriture
/// des rubriques &amp; formules) : ports <see cref="IRubriqueRepository"/> et
/// <see cref="IClock"/> mockés ; les use cases et le ViewModel sont réels.
/// </summary>
public class EditerRubriqueViewModelTests
{
    private static EditerRubriqueViewModel Build(
        out Mock<IRubriqueRepository> rubriques, out Mock<IDialogService> dialogs)
    {
        rubriques = new Mock<IRubriqueRepository>();
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        dialogs = new Mock<IDialogService>();
        var navigation = new Mock<PaieEducation.Presentation.Navigation.INavigationService>();

        var definirRubrique = new DefinirRubrique(rubriques.Object, clock.Object);
        var definirFormule = new DefinirFormuleRubrique(rubriques.Object, clock.Object);
        var definirParametre = new DefinirParametreRubrique(rubriques.Object, clock.Object);
        return new EditerRubriqueViewModel(definirRubrique, definirFormule, definirParametre, dialogs.Object, navigation.Object);
    }

    [Fact]
    public void ValiderFormule_expression_valide_affiche_message_positif()
    {
        var vm = Build(out _, out _);
        vm.FormuleExpression = "TBASE * 0.45";

        vm.ValiderFormuleCommand.Execute(null);

        Assert.Contains("valide", vm.FormuleValidation);
    }

    [Fact]
    public void ValiderFormule_expression_invalide_affiche_message_clair()
    {
        var vm = Build(out _, out _);
        vm.FormuleExpression = "TBASE * * 0.45";

        vm.ValiderFormuleCommand.Execute(null);

        Assert.Contains("invalide", vm.FormuleValidation);
    }

    [Fact]
    public async Task DefinirIdentite_avec_ordre_invalide_affiche_erreur_et_ne_appelle_pas_le_repo()
    {
        var vm = Build(out var rubriques, out var dialogs);
        vm.RubriqueId = "ISSRP_45";
        vm.OrdreCalcul = "abc";

        await vm.DefinirIdentiteCommand.ExecuteAsync(null);

        rubriques.Verify(r => r.DefinirRubriqueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DefinirIdentite_nominal_appelle_le_repo_et_affiche_resultat()
    {
        var vm = Build(out var rubriques, out var dialogs);
        vm.RubriqueId = "ISSRP_45";
        vm.Libelle = "Soutien scolaire 45%";
        vm.OrdreCalcul = "10";
        rubriques.Setup(r => r.DefinirRubriqueAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success("ISSRP_45"));

        await vm.DefinirIdentiteCommand.ExecuteAsync(null);

        rubriques.Verify(r => r.DefinirRubriqueAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("ISSRP_45", vm.IdentiteResultat ?? string.Empty);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DefinirFormule_formule_invalide_affiche_erreur_sans_appeler_le_repo()
    {
        var vm = Build(out var rubriques, out var dialogs);
        vm.FormuleRubriqueId = "ISSRP_45";
        vm.FormuleExpression = "TBASE * * 0.45";
        vm.FormuleDateEffet = "2026-01-01";

        await vm.DefinirFormuleCommand.ExecuteAsync(null);

        rubriques.Verify(r => r.DefinirFormuleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DefinirFormule_formule_valide_appelle_le_repo()
    {
        var vm = Build(out var rubriques, out var dialogs);
        vm.FormuleRubriqueId = "ISSRP_45";
        vm.FormuleExpression = "TBASE * 0.45";
        vm.FormuleDateEffet = "2026-01-01";
        rubriques.Setup(r => r.DefinirFormuleAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success("RF-ISSRP_45-2026-01-01"));

        await vm.DefinirFormuleCommand.ExecuteAsync(null);

        rubriques.Verify(r => r.DefinirFormuleAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DefinirParametre_nominal_appelle_le_repo()
    {
        var vm = Build(out var rubriques, out var dialogs);
        vm.ParametreRubriqueId = "MUNATEC";
        vm.ParametreCle = "TAUX";
        vm.ParametreValeur = "1.0";
        vm.ParametreDateEffet = "2008-01-01";
        rubriques.Setup(r => r.DefinirParametreAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success("RP-MUNATEC-TAUX-2008-01-01"));

        await vm.DefinirParametreCommand.ExecuteAsync(null);

        rubriques.Verify(r => r.DefinirParametreAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }
}
