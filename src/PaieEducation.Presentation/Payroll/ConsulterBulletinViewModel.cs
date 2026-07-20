using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Rappels;
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
/// Excel via <see cref="IExporterBulletin"/> (chantier C3), ainsi que la
/// restitution des rappels rattachés via <see cref="ListerRappels"/> (P9) —
/// jusqu'ici générés (D9) mais invisibles à l'écran.
/// </summary>
public sealed partial class ConsulterBulletinViewModel : ObservableObject
{
    private readonly ConsulterBulletin _consulterBulletin;
    private readonly ListerRappels _listerRappels;
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

    public ObservableCollection<LigneRappel> Rappels { get; } = [];

    public bool HasBulletin => Bulletin is not null;

    partial void OnBulletinChanged(Bulletin? value) => OnPropertyChanged(nameof(HasBulletin));

    public ConsulterBulletinViewModel(
        ConsulterBulletin consulterBulletin, ListerRappels listerRappels, IExporterBulletin exporter, IDialogService dialogs)
    {
        _consulterBulletin = consulterBulletin ?? throw new ArgumentNullException(nameof(consulterBulletin));
        _listerRappels = listerRappels ?? throw new ArgumentNullException(nameof(listerRappels));
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    [RelayCommand]
    private async Task ConsulterAsync()
    {
        EnCours = true;
        Resultat = null;
        PeutExporter = false;
        Rappels.Clear();
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

            var rappels = await _listerRappels.ExecuterAsync(new ListerRappels.Demande(AgentId, DatePaie));
            if (rappels.IsSuccess)
            {
                foreach (var ligne in rappels.Value)
                    Rappels.Add(ligne);
            }
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

