using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Presentation.Dialogs;

namespace PaieEducation.Presentation.Payroll;

/// <summary>
/// Écran « Consulter un bulletin » (Phase 6, tâche 3) — relit le bulletin
/// déjà validé d'un agent via le use case <see cref="ConsulterBulletin"/>
/// (déjà livré, Phase 5). Lecture seule : ne recalcule jamais (ADR-0008).
/// </summary>
public sealed partial class ConsulterBulletinViewModel : ObservableObject
{
    private readonly ConsulterBulletin _consulterBulletin;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private string agentId = string.Empty;

    [ObservableProperty]
    private string datePaie = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");

    [ObservableProperty]
    private string? resultat;

    [ObservableProperty]
    private bool enCours;

    public ConsulterBulletinViewModel(ConsulterBulletin consulterBulletin, IDialogService dialogs)
    {
        _consulterBulletin = consulterBulletin ?? throw new ArgumentNullException(nameof(consulterBulletin));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    [RelayCommand]
    private async Task ConsulterAsync()
    {
        EnCours = true;
        Resultat = null;
        try
        {
            var demande = new ConsulterBulletin.Demande(AgentId, DatePaie);
            var result = await _consulterBulletin.ExecuterAsync(demande);
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }

            var b = result.Value;
            Resultat = $"Net : {b.Net:N0} DA — Total gains : {b.TotalGains:N0} DA — IRG : {b.Irg:N0} DA";
        }
        finally
        {
            EnCours = false;
        }
    }
}
