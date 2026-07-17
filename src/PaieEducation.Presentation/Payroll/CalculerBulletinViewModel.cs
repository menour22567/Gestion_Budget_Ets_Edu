using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Presentation.Dialogs;

namespace PaieEducation.Presentation.Payroll;

/// <summary>
/// Écran « Calculer un bulletin » (Phase 6, tâche 1) — premier écran réel du
/// Shell, preuve de la chaîne complète WPF → ViewModel → Application →
/// Infrastructure → SQLite via le use case <see cref="CalculerBulletin"/>
/// (déjà livré, Phase 5).
/// </summary>
/// <remarks>
/// <see cref="ClesBareme"/>/<see cref="SourcesValeur"/> ne sont pas encore
/// résolus automatiquement par aucun use case (dette connue depuis Phase 5) —
/// cet écran utilise les mêmes valeurs fixes que tous les tests d'intégration
/// de la session (<c>CATEGORIE=13</c>, <c>PAPP=0.30</c>) plutôt que d'inventer
/// un formulaire de paramétrage complet, hors périmètre de cette tranche.
/// </remarks>
public sealed partial class CalculerBulletinViewModel : ObservableObject
{
    private static readonly IReadOnlyDictionary<string, decimal> SourcesValeur =
        new Dictionary<string, decimal> { ["PAPP"] = 0.30m };
    private static readonly IReadOnlyDictionary<string, string> ClesBareme =
        new Dictionary<string, string> { ["CATEGORIE"] = "13" };

    private readonly CalculerBulletin _calculerBulletin;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private string agentId = string.Empty;

    [ObservableProperty]
    private string datePaie = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");

    [ObservableProperty]
    private string? resultat;

    [ObservableProperty]
    private bool enCours;

    public CalculerBulletinViewModel(CalculerBulletin calculerBulletin, IDialogService dialogs)
    {
        _calculerBulletin = calculerBulletin ?? throw new ArgumentNullException(nameof(calculerBulletin));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    [RelayCommand]
    private async Task CalculerAsync()
    {
        EnCours = true;
        Resultat = null;
        try
        {
            var demande = new CalculerBulletin.Demande(AgentId, DatePaie, SourcesValeur, ClesBareme, ProfilFiscal.Standard);
            var result = await _calculerBulletin.ExecuterAsync(demande);
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
