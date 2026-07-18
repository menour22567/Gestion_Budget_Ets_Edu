using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Reporting;
using PaieEducation.Reporting.UseCases;
using System.IO;

namespace PaieEducation.Presentation.Payroll;

/// <summary>
/// Écran « Consulter un bulletin » (Phase 6, tâche 3) — relit le bulletin
/// déjà validé d'un agent via le use case <see cref="ConsulterBulletin"/>
/// (déjà livré, Phase 5). Lecture seule : ne recalcule jamais (ADR-0008).
/// Ajoute l'export du bulletin validé (snapshot immuable) au format PDF ou
/// Excel via <see cref="IExporterBulletin"/> (chantier C3).
/// </summary>
public sealed partial class ConsulterBulletinViewModel : ObservableObject
{
    private readonly ConsulterBulletin _consulterBulletin;
    private readonly IExporterBulletin _exporter;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private string agentId = string.Empty;

    [ObservableProperty]
    private string datePaie = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");

    [ObservableProperty]
    private string? resultat;

    [ObservableProperty]
    private bool enCours;

    [ObservableProperty]
    private bool peutExporter;

    [ObservableProperty]
    private Bulletin? bulletin;

    public bool HasBulletin => Bulletin is not null;

    partial void OnBulletinChanged(Bulletin? value) => OnPropertyChanged(nameof(HasBulletin));

    public ConsulterBulletinViewModel(
        ConsulterBulletin consulterBulletin, IExporterBulletin exporter, IDialogService dialogs)
    {
        _consulterBulletin = consulterBulletin ?? throw new ArgumentNullException(nameof(consulterBulletin));
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    [RelayCommand]
    private async Task ConsulterAsync()
    {
        EnCours = true;
        Resultat = null;
        PeutExporter = false;
        try
        {
            var demande = new ConsulterBulletin.Demande(AgentId, DatePaie);
            var result = await _consulterBulletin.ExecuterAsync(demande);
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }

            Bulletin = result.Value;
            var b = Bulletin;
            Resultat = $"Net : {b.Net:N0} DA — Total gains : {b.TotalGains:N0} DA — IRG : {b.Irg:N0} DA";
            PeutExporter = true;
        }
        finally
        {
            EnCours = false;
        }
    }

    [RelayCommand]
    private async Task ExporterAsync(FormatDocument format)
    {
        if (!PeutExporter) return;
        try
        {
            var dossier = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PaieEducation", "Exports");
            Directory.CreateDirectory(dossier);
            var nom = $"Bulletin_{AgentId}_{DatePaie.Replace("-", "")}";
            var chemin = Path.Combine(dossier, nom);

            var demande = new Demande(AgentId, DatePaie, format, chemin);
            var result = await _exporter.ExecuterAsync(demande);
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }

            Resultat = $"Document généré : {result.Value}";
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync($"Échec de l'export : {ex.Message}");
        }
    }
}

