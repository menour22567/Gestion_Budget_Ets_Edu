using Microsoft.Extensions.DependencyInjection;

namespace PaieEducation.Presentation.Navigation;

/// <summary>Implémentation DI de <see cref="INavigationService"/>.</summary>
public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;

    public NavigationService(IServiceProvider services)
        => _services = services ?? throw new ArgumentNullException(nameof(services));

    public event Action<object>? ViewModelChanged;

    public void NavigateTo<TViewModel>() where TViewModel : class
        => NavigateTo<TViewModel>(_ => { });

    public void NavigateTo<TViewModel>(Action<TViewModel> configurer) where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(configurer);
        var viewModel = _services.GetRequiredService<TViewModel>();
        configurer(viewModel);
        ViewModelChanged?.Invoke(viewModel);
    }
}
