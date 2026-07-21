using Moq;
using PaieEducation.Presentation.Navigation;

namespace PaieEducation.Tests.Presentation;

/// <summary>Tests de <see cref="NavigationService"/> (Shell à onglets multi-documents).</summary>
public class NavigationServiceTests
{
    private sealed class EcranFactice
    {
        public string? Valeur { get; set; }
    }

    private static NavigationService BuildNavigationService(EcranFactice ecran)
    {
        var services = new Mock<IServiceProvider>();
        services.Setup(s => s.GetService(typeof(EcranFactice))).Returns(ecran);
        return new NavigationService(services.Object);
    }

    [Fact]
    public void OpenTab_resout_via_le_service_provider_et_leve_TabRequested_avec_le_titre()
    {
        var ecran = new EcranFactice();
        var navigation = BuildNavigationService(ecran);
        TabRequest? demandeCapturee = null;
        navigation.TabRequested += r => demandeCapturee = r;

        navigation.OpenTab<EcranFactice>("Écran factice");

        Assert.NotNull(demandeCapturee);
        Assert.Equal("Écran factice", demandeCapturee!.Titre);
        Assert.Same(ecran, demandeCapturee.ViewModel);
    }

    [Fact]
    public void OpenTab_avec_configurer_initialise_l_instance_avant_de_lever_TabRequested()
    {
        var ecran = new EcranFactice();
        var navigation = BuildNavigationService(ecran);
        TabRequest? demandeCapturee = null;
        navigation.TabRequested += r => demandeCapturee = r;

        navigation.OpenTab<EcranFactice>("Écran factice", e => e.Valeur = "préchargé");

        Assert.Equal("préchargé", ecran.Valeur);
        Assert.NotNull(demandeCapturee);
    }
}
