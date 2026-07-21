using Moq;
using PaieEducation.Presentation.Navigation;
using PaieEducation.Presentation.Shell;

namespace PaieEducation.Tests.Presentation;

/// <summary>Tests de <see cref="ShellViewModel"/> (Shell à onglets multi-documents).</summary>
public class ShellViewModelTests
{
    private static ShellViewModel BuildViewModel(out Mock<INavigationService> navigation)
    {
        navigation = new Mock<INavigationService>();
        var accueil = new AccueilViewModel(navigation.Object);
        return new ShellViewModel(navigation.Object, accueil);
    }

    [Fact]
    public void Au_demarrage_un_seul_onglet_Accueil_non_fermable_est_ouvert_et_actif()
    {
        var vm = BuildViewModel(out _);

        var onglet = Assert.Single(vm.Onglets);
        Assert.Equal("Accueil", onglet.Titre);
        Assert.False(onglet.EstFermable);
        Assert.Same(onglet, vm.OngletActif);
    }

    [Fact]
    public void Une_demande_TabRequested_ouvre_un_nouvel_onglet_fermable_et_l_active()
    {
        var vm = BuildViewModel(out var navigation);
        var ecran = new object();

        navigation.Raise(n => n.TabRequested += null, new TabRequest("Calculer un bulletin", ecran));

        Assert.Equal(2, vm.Onglets.Count);
        Assert.Same(vm.Onglets[1], vm.OngletActif);
        Assert.True(vm.OngletActif!.EstFermable);
        Assert.Same(ecran, vm.OngletActif.Contenu);
    }

    [Fact]
    public void Fermer_l_onglet_actif_selectionne_un_voisin_sans_jamais_vider_la_collection()
    {
        var vm = BuildViewModel(out var navigation);
        navigation.Raise(n => n.TabRequested += null, new TabRequest("Onglet A", new object()));
        navigation.Raise(n => n.TabRequested += null, new TabRequest("Onglet B", new object()));

        var ongletB = vm.Onglets[2];
        Assert.Same(ongletB, vm.OngletActif);

        ongletB.FermerCommand.Execute(null);

        Assert.Equal(2, vm.Onglets.Count);
        Assert.Same(vm.Onglets[1], vm.OngletActif);

        vm.Onglets[1].FermerCommand.Execute(null);

        var accueilRestant = Assert.Single(vm.Onglets);
        Assert.Same(accueilRestant, vm.OngletActif);
        Assert.False(accueilRestant.EstFermable);
    }
}
