using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Domain.Calcul.Audit;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Domain.Calcul.Explicabilite;

namespace PaieEducation.Presentation.Payroll;

/// <summary>
/// Écran « Calculer un bulletin » (Phase 6, tâche 1) — premier écran réel du
/// Shell, preuve de la chaîne complète WPF → ViewModel → Application →
/// Infrastructure → SQLite via le use case <see cref="CalculerBulletin"/>
/// (déjà livré, Phase 5).
/// </summary>
/// <remarks>
/// C2-UI.2 — sélecteur d'agent (liste issue de <see cref="IAgentReadRepository"/>)
/// à la place de la saisie libre d'identifiant, et affichage détaillé du bulletin
/// calculé : lignes (nature + montant) avec explication traçable par ligne
/// (<see cref="ExplicationLigne"/>, RM-105) et journal d'audit (<see cref="JournalAudit"/>).
/// <br/>
/// Conformément au chantier C2 (auto-résolution des entrées, lot C2.3), le
/// use case résout tout depuis le dossier agent (<see cref="CalculEntreeResolver"/>)
/// et les paramètres système (arrondi). Zéro hardcoding sur le chemin de calcul.
/// </remarks>
public sealed partial class CalculerBulletinViewModel : ObservableObject
{
    private readonly CalculerBulletin _calculerBulletin;
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
    private ObservableCollection<LigneBulletinVue> lignes = [];

    [ObservableProperty]
    private decimal totalGains;

    [ObservableProperty]
    private decimal assietteCotisable;

    [ObservableProperty]
    private decimal assietteImposable;

    [ObservableProperty]
    private decimal totalRetenues;

    [ObservableProperty]
    private decimal irg;

    [ObservableProperty]
    private decimal net;

    [ObservableProperty]
    private ObservableCollection<string> audit = [];

    [ObservableProperty]
    private bool enCours;

    [ObservableProperty]
    private Bulletin? bulletin;

    public CalculerBulletinViewModel(
        CalculerBulletin calculerBulletin, IAgentReadRepository agentsRead, IDialogService dialogs)
    {
        _calculerBulletin = calculerBulletin ?? throw new ArgumentNullException(nameof(calculerBulletin));
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
    private async Task CalculerAsync()
    {
        EnCours = true;
        Lignes = [];
        Audit = [];
        try
        {
            // C2.3 — aucune entrée fournie : le use case auto-résout les clés de
            // barème et les sources de valeur depuis le dossier agent, et le mode
            // d'arrondi depuis Parametres.
            var demande = new CalculerBulletin.Demande(AgentId, DatePaie, null, null, ProfilFiscal.Standard);
            var result = await _calculerBulletin.ExecuterAsync(demande);
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }

            Afficher(result.Value);
        }
        finally
        {
            EnCours = false;
        }
    }

    private void Afficher(Bulletin b)
    {
        var lignesVue = new ObservableCollection<LigneBulletinVue>();
        foreach (var l in b.Lignes)
            lignesVue.Add(new LigneBulletinVue(l));

        Lignes = lignesVue;
        TotalGains = b.TotalGains.Amount;
        AssietteCotisable = b.AssietteCotisable.Amount;
        AssietteImposable = b.AssietteImposable.Amount;
        TotalRetenues = b.TotalRetenues.Amount;
        Irg = b.Irg.Amount;
        Net = b.Net.Amount;

        Bulletin = b;

        var auditVue = new ObservableCollection<string>();
        foreach (var e in b.Audit.Etapes)
        {
            var verdict = e.Eligible ? "éligible" : "non éligible";
            var montant = e.Montant is { } m ? $" = {m:N0} DA" : " (aucun montant)";
            auditVue.Add($"#{e.Ordre} {e.RubriqueId} — {verdict}{montant}");
        }
        Audit = auditVue;
    }
}

/// <summary>Ligne de bulletin projetée pour l'affichage détaillé (MVVM, sans logique de calcul).</summary>
public sealed class LigneBulletinVue
{
    public string RubriqueId { get; }
    public string Nature { get; }
    public decimal Montant { get; }
    public string Explication { get; }

    public LigneBulletinVue(BulletinLigne ligne)
    {
        RubriqueId = ligne.RubriqueId;
        Nature = ligne.Nature.ToString();
        Montant = ligne.Montant.Amount;
        Explication = ligne.Explication.Rendu();
    }
}
