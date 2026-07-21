using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Presentation.Navigation;

namespace PaieEducation.Presentation.Agents;

/// <summary>
/// Écran « Liste des agents » (chantier gestion des agents) — affiche tous les
/// agents (<see cref="IAgentReadRepository.ListerAsync"/>, déjà consommé par
/// l'écran « Calculer un bulletin »), avec un filtre plein texte (matricule /
/// nom / prénom) et un drill-down vers la fiche détaillée
/// (<see cref="FicheAgentViewModel"/>, navigation préchargée).
/// </summary>
/// <remarks>
/// Le filtre est appliqué côté client sur la liste chargée une fois au montage
/// (populations d'agents attendues modestes) : <see cref="Agents"/> est la vue
/// filtrée, <see cref="_tous"/> la source complète. Aucune écriture ici — la
/// création reste l'écran « Créer un agent », l'édition viendra plus tard.
/// </remarks>
public sealed partial class ListeAgentsViewModel : ObservableObject
{
    private readonly IAgentReadRepository _agentsRead;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly List<AgentResume> _tous = [];

    public ObservableCollection<AgentResume> Agents { get; } = [];

    [ObservableProperty] private string filtre = string.Empty;
    [ObservableProperty] private bool enCours;

    public ListeAgentsViewModel(
        IAgentReadRepository agentsRead, INavigationService navigation, IDialogService dialogs)
    {
        _agentsRead = agentsRead ?? throw new ArgumentNullException(nameof(agentsRead));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        _ = ChargerCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task ChargerAsync()
    {
        EnCours = true;
        try
        {
            var result = await _agentsRead.ListerAsync();
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }

            _tous.Clear();
            _tous.AddRange(result.Value);
            AppliquerFiltre();
        }
        finally
        {
            EnCours = false;
        }
    }

    partial void OnFiltreChanged(string value) => AppliquerFiltre();

    private void AppliquerFiltre()
    {
        var f = Filtre?.Trim() ?? string.Empty;
        Agents.Clear();
        foreach (var a in _tous)
        {
            if (f.Length == 0
                || a.Matricule.Contains(f, StringComparison.OrdinalIgnoreCase)
                || a.Nom.Contains(f, StringComparison.OrdinalIgnoreCase)
                || a.Prenom.Contains(f, StringComparison.OrdinalIgnoreCase))
            {
                Agents.Add(a);
            }
        }
    }

    [RelayCommand]
    private void Consulter(string? agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId)) return;
        _navigation.OpenTab<FicheAgentViewModel>("Fiche agent", fiche =>
        {
            fiche.AgentId = agentId;
            _ = fiche.ChargerCommand.ExecuteAsync(null);
        });
    }
}
