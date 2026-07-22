using Moq;
using PaieEducation.Application.Workbench.Services;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Tests.Integration.Application;

/// <summary>
/// Tests du provider d'auto-complétion de formule (P10 — FormulaEditor avancé).
/// On mocke <see cref="IWorkbenchReadRepository"/> pour isoler le catalogue
/// statique (filtre, tri, plafond) du bruit I/O base. La lecture des
/// rubriques actives est couverte en intégration « bout-en-bout » par
/// d'autres tests ; ici on vérifie surtout la logique de filtrage et
/// l'ordre de tri stable (Fonction &lt; Variable &lt; Source &lt; Rubrique).
/// </summary>
public class FormuleCompletionProviderTests
{
    private static FormuleCompletionProvider BuildProvider(
        Mock<IWorkbenchReadRepository> workbench)
        => new(workbench.Object);

    [Fact]
    public async Task ProposerAsync_prefixe_vide_renvoie_tout_le_catalogue_filtre_au_max()
    {
        // Choix de design : préfixe vide = on retourne tout (filtré par
        // StartsWith("") qui est true pour tout). Utile si l'utilisateur
        // clique "Proposer" sans avoir tapé de lettres. Le popup se charge
        // de plafonner l'affichage.
        var workbench = new Mock<IWorkbenchReadRepository>(MockBehavior.Strict);
        workbench.Setup(w => w.ListerRubriquesActivesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RubriqueResume>());
        var provider = BuildProvider(workbench);

        var items = await provider.ProposerAsync(string.Empty, max: 100);

        Assert.NotEmpty(items);
        // 6 fonctions + 7 variables + 8 sources = 21 items dans le catalogue statique.
        Assert.True(items.Count >= 21);
    }

    [Fact]
    public async Task ProposerAsync_prefixe_round_retourne_la_fonction_round_en_premier()
    {
        var workbench = new Mock<IWorkbenchReadRepository>(MockBehavior.Strict);
        workbench.Setup(w => w.ListerRubriquesActivesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RubriqueResume>());
        var provider = BuildProvider(workbench);

        var items = await provider.ProposerAsync("rou");

        Assert.NotEmpty(items);
        Assert.Equal("round(", items[0].Token);
        Assert.Equal(CompletionCategorie.Fonction, items[0].Categorie);
    }

    [Fact]
    public async Task ProposerAsync_insensible_a_la_casse_pour_le_prefixe()
    {
        var workbench = new Mock<IWorkbenchReadRepository>(MockBehavior.Strict);
        workbench.Setup(w => w.ListerRubriquesActivesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RubriqueResume>());
        var provider = BuildProvider(workbench);

        var lower = await provider.ProposerAsync("rou");
        var upper = await provider.ProposerAsync("ROU");
        var mixed = await provider.ProposerAsync("RoU");

        Assert.Equal(lower.Count, upper.Count);
        Assert.Equal(lower.Count, mixed.Count);
        Assert.Equal(lower[0].Token, upper[0].Token);
    }

    [Fact]
    public async Task ProposerAsync_prefixe_indisponible_renvoie_liste_vide()
    {
        var workbench = new Mock<IWorkbenchReadRepository>(MockBehavior.Strict);
        workbench.Setup(w => w.ListerRubriquesActivesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RubriqueResume>());
        var provider = BuildProvider(workbench);

        var items = await provider.ProposerAsync("zzzzz_no_match");

        Assert.Empty(items);
    }

    [Fact]
    public async Task ProposerAsync_les_fonction_sont_classees_avant_les_variables()
    {
        var workbench = new Mock<IWorkbenchReadRepository>(MockBehavior.Strict);
        workbench.Setup(w => w.ListerRubriquesActivesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RubriqueResume>());
        var provider = BuildProvider(workbench);

        // Préfixe vide → tout le catalogue. Le premier item doit être une
        // fonction (catégorie 0), pas une variable.
        var items = await provider.ProposerAsync(string.Empty, max: 100);

        Assert.NotEmpty(items);
        Assert.Equal(CompletionCategorie.Fonction, items[0].Categorie);
    }

    [Fact]
    public async Task ProposerAsync_integre_les_rubriques_actives_de_la_base()
    {
        var workbench = new Mock<IWorkbenchReadRepository>();
        workbench.Setup(w => w.ListerRubriquesActivesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RubriqueResume("ISSRP_45", "Soutien scolaire 45%"),
                new RubriqueResume("QUALIF", "Indemnité de qualification"),
            });
        var provider = BuildProvider(workbench);

        var items = await provider.ProposerAsync("QUAL");

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.Categorie == CompletionCategorie.Rubrique && i.Token == "QUALIF");
    }

    [Fact]
    public async Task ProposerAsync_echec_du_repo_rend_le_catalogue_statique_seul()
    {
        var workbench = new Mock<IWorkbenchReadRepository>();
        workbench.Setup(w => w.ListerRubriquesActivesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB indisponible"));
        var provider = BuildProvider(workbench);

        // Ne doit pas lever d'exception : le catch silencieux dans le provider
        // garantit qu'un incident I/O ne casse pas l'auto-complétion.
        var items = await provider.ProposerAsync("rou");

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.Categorie == CompletionCategorie.Fonction);
    }

    [Fact]
    public async Task ProposerAsync_plafonne_les_resultats_a_max()
    {
        var workbench = new Mock<IWorkbenchReadRepository>();
        workbench.Setup(w => w.ListerRubriquesActivesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RubriqueResume>());
        var provider = BuildProvider(workbench);

        var items = await provider.ProposerAsync(string.Empty, max: 3);

        Assert.True(items.Count <= 3);
    }

    [Fact]
    public async Task ProposerAsync_avec_max_negatif_leve_une_exception()
    {
        var workbench = new Mock<IWorkbenchReadRepository>(MockBehavior.Strict);
        var provider = BuildProvider(workbench);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => provider.ProposerAsync("rou", max: -1));
    }
}
