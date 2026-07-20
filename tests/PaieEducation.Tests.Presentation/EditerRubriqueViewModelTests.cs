using Moq;
using PaieEducation.Application.Referentiels.UseCases;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Workbench.Repositories;
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
        => Build(out rubriques, out _, out dialogs);

    private static EditerRubriqueViewModel Build(
        out Mock<IRubriqueRepository> rubriques, out Mock<IRubriqueBaremeRepository> baremes, out Mock<IDialogService> dialogs)
    {
        rubriques = new Mock<IRubriqueRepository>();
        baremes = new Mock<IRubriqueBaremeRepository>();
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        dialogs = new Mock<IDialogService>();
        var navigation = new Mock<PaieEducation.Presentation.Navigation.INavigationService>();

        var definirRubrique = new DefinirRubrique(rubriques.Object, clock.Object);
        var definirFormule = new DefinirFormuleRubrique(rubriques.Object, clock.Object);
        var definirParametre = new DefinirParametreRubrique(rubriques.Object, clock.Object);
        var definirBareme = new DefinirValeurBareme(baremes.Object, clock.Object);
        return new EditerRubriqueViewModel(definirRubrique, definirFormule, definirParametre, definirBareme, dialogs.Object, navigation.Object);
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

    [Fact]
    public async Task DefinirBareme_champs_requis_absents_affiche_erreur_sans_appeler_le_repo()
    {
        var vm = Build(out _, out var baremes, out var dialogs);
        vm.BaremeRubriqueId = string.Empty;
        vm.BaremeBorneInf = string.Empty;

        await vm.DefinirBaremeCommand.ExecuteAsync(null);

        baremes.Verify(b => b.DefinirValeurBaremeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>(), It.IsAny<PaieEducation.Domain.Common.IUnitOfWork?>()), Times.Never);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DefinirBareme_nominal_appelle_le_repo_avec_la_dimension_et_le_type_par_defaut()
    {
        var vm = Build(out _, out var baremes, out var dialogs);
        vm.BaremeRubriqueId = "QUALIF";
        vm.BaremeBorneInf = "13";
        vm.BaremeValeur = "0.45";
        vm.BaremeDateEffet = "2026-01-01";
        baremes.Setup(b => b.DefinirValeurBaremeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>(), It.IsAny<PaieEducation.Domain.Common.IUnitOfWork?>()))
            .ReturnsAsync(Result.Success("RB-QUALIF-CATEGORIE-13-2026-01-01"));

        await vm.DefinirBaremeCommand.ExecuteAsync(null);

        baremes.Verify(b => b.DefinirValeurBaremeAsync(
                "QUALIF", "CATEGORIE", "13", null, "TAUX", "0.45", "2026-01-01", null,
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>(), It.IsAny<PaieEducation.Domain.Common.IUnitOfWork?>()),
            Times.Once);
        Assert.Contains("RB-QUALIF-CATEGORIE-13-2026-01-01", vm.BaremeResultat ?? string.Empty);
        dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DefinirBareme_echec_du_repo_affiche_l_erreur()
    {
        var vm = Build(out _, out var baremes, out var dialogs);
        vm.BaremeRubriqueId = "QUALIF";
        vm.BaremeBorneInf = "13";
        vm.BaremeValeur = "0.45";
        vm.BaremeDateEffet = "2019-01-01";
        baremes.Setup(b => b.DefinirValeurBaremeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>(), It.IsAny<PaieEducation.Domain.Common.IUnitOfWork?>()))
            .ReturnsAsync(Result.Failure<string>(Error.Validation("Date antérieure à la version en vigueur.")));

        await vm.DefinirBaremeCommand.ExecuteAsync(null);

        Assert.Null(vm.BaremeResultat);
        dialogs.Verify(d => d.ShowErrorAsync(It.Is<string>(m => m.Contains("antérieure"))), Times.Once);
    }
}
