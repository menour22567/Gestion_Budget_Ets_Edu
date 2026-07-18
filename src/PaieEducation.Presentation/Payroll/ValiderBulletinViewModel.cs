using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Application.Payroll.UseCases;
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
/// d'arrondi depuis Parametres.
/// </remarks>
public sealed partial class ValiderBulletinViewModel : ObservableObject
{
    private readonly ValiderBulletin _validerBulletin;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private string agentId = string.Empty;

    [ObservableProperty]
    private string datePaie = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");

    [ObservableProperty]
    private string? resultat;

    [ObservableProperty]
    private bool enCours;

    public ValiderBulletinViewModel(ValiderBulletin validerBulletin, IDialogService dialogs)
    {
        _validerBulletin = validerBulletin ?? throw new ArgumentNullException(nameof(validerBulletin));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
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
