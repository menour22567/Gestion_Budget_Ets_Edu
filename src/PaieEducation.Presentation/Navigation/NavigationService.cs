using Microsoft.Extensions.DependencyInjection;

namespace PaieEducation.Presentation.Navigation;

/// <summary>Implémentation DI de <see cref="INavigationService"/>.</summary>
public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;

    public NavigationService(IServiceProvider services)
        => _services = services ?? throw new ArgumentNullException(nameof(services));

    public event Action<TabRequest>? TabRequested;

    public void OpenTab<TViewModel>(string titre) where TViewModel : class
        => OpenTab<TViewModel>(titre, _ => { });

    public void OpenTab<TViewModel>(string titre, Action<TViewModel> configurer) where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(titre);
        ArgumentNullException.ThrowIfNull(configurer);
        var viewModel = _services.GetRequiredService<TViewModel>();
        configurer(viewModel);
        TabRequested?.Invoke(new TabRequest(titre, viewModel));
    }
}
