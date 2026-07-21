using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Presentation.Dialogs;

namespace PaieEducation.Presentation.Payroll;

/// <summary>
/// Écran « Valider un bulletin » (Phase 6, tâche 3) — calcule et **fige**
/// (Snapshot Engine, ADR-0008) le bulletin d'un agent via le use case
/// <see cref="ValiderBulletin"/> (déjà livré, Phase 5). Un bulletin déjà
/// validé pour cet agent/cette date ne peut jamais être réécrit — la
/// tentative échoue explicitement (message affiché via
/// <see cref="IDialogService.ShowErrorAsync"/>), jamais silencieusement.
/// </summary>
/// <remarks>
/// C2.1/C2.2/C2.3 — aucune valeur hardcodée : le use case auto-résout les
/// clés de barème et sources de valeur depuis le dossier agent, et le mode
/// d'arrondi depuis Parametres. Sélecteur d'agent (<see cref="Agents"/>,
/// même patron que <c>CalculerBulletinViewModel</c>) plutôt qu'une saisie
/// libre de GUID.
/// </remarks>
public sealed partial class ValiderBulletinViewModel : ObservableObject
{
    private readonly ValiderBulletin _validerBulletin;
    private readonly IAgentReadRepository _agentsRead;
    private readonly IDialogService _dialogs;

    public ObservableCollection<AgentResume> Agents { get; } = [];

    [ObservableProperty]
    private AgentResume? agentSelectionne;

    [ObservableProperty]
    private string agentId = string.Empty;

    [ObservableProperty]
    private string datePaie = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");

    [ObservableProperty]
    private string? resultat;

    [ObservableProperty]
    private bool enCours;

    public ValiderBulletinViewModel(ValiderBulletin validerBulletin, IAgentReadRepository agentsRead, IDialogService dialogs)
    {
        _validerBulletin = validerBulletin ?? throw new ArgumentNullException(nameof(validerBulletin));
        _agentsRead = agentsRead ?? throw new ArgumentNullException(nameof(agentsRead));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        _ = ChargerAgentsCommand.ExecuteAsync(null);
    }

    /// <summary>Reflète la sélection du ComboBox dans l'identifiant utilisé par le use case.</summary>
    partial void OnAgentSelectionneChanged(AgentResume? value) => AgentId = value?.Id ?? string.Empty;

    [RelayCommand]
    private async Task ChargerAgentsAsync()
    {
        var result = await _agentsRead.ListerAsync();
        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync(result.Error.Message);
            return;
        }

        Agents.Clear();
        foreach (var a in result.Value)
            Agents.Add(a);
    }

    [RelayCommand]
    private async Task ValiderAsync()
    {
        EnCours = true;
        Resultat = null;
        try
        {
            // C2.2/C2.3 — aucune entrée fournie : le use case auto-résout
            // les clés de barème et les sources de valeur depuis le dossier agent.
            var demande = new CalculerBulletin.Demande(AgentId, DatePaie, null, null, ProfilFiscal.Standard);
            var result = await _validerBulletin.ExecuterAsync(demande);
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }

            Resultat = $"Bulletin validé (Id : {result.Value})";
        }
        finally
        {
            EnCours = false;
        }
    }
}
