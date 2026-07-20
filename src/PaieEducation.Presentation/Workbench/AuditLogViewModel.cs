using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Shared.Results;

namespace PaieEducation.Presentation.Workbench;

/// <summary>
/// Écran « Audit &amp; traçabilité » (Phase 6, tâche 4 ; filtres et pagination
/// chantier P4). Filtres acteur/action/type d'entité/période combinés en ET
/// (<see cref="FiltreAuditLog"/>), pagination incrémentale (« Charger plus »)
/// — plus de plafond fixe silencieux (l'ancien <c>LIMIT 500</c> en dur).
/// </summary>
/// <remarks>
/// Dates saisies en texte <c>yyyy-MM-dd</c> (même convention que
/// <c>CalculerBulletinViewModel.DatePaie</c>, pas de <c>DatePicker</c>) ; une
/// date non vide mais mal formée est signalée par erreur plutôt que
/// silencieusement ignorée. <see cref="ChargerCommand"/> applique les filtres
/// courants et réinitialise la pagination à la page 1 (invoqué en
/// fire-and-forget au montage, patron déjà établi) ; <see cref="ChargerPlusCommand"/>
/// ajoute la page suivante sans réinitialiser la liste.
/// </remarks>
public sealed partial class AuditLogViewModel : ObservableObject
{
    private readonly ListerAuditLog _listerAuditLog;
    private readonly IDialogService _dialogs;

    private int _page = 1;

    [ObservableProperty]
    private bool enCours;

    [ObservableProperty]
    private string? filtreActeur;

    [ObservableProperty]
    private string? filtreAction;

    [ObservableProperty]
    private string? filtreTypeEntite;

    [ObservableProperty]
    private string filtreDateDebut = string.Empty;

    [ObservableProperty]
    private string filtreDateFin = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChargerPlusCommand))]
    private bool peutChargerPlus;

    public ObservableCollection<EntreeAuditLog> Entrees { get; } = [];

    public AuditLogViewModel(ListerAuditLog listerAuditLog, IDialogService dialogs)
    {
        _listerAuditLog = listerAuditLog ?? throw new ArgumentNullException(nameof(listerAuditLog));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        _ = ChargerCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task ChargerAsync()
    {
        _page = 1;
        Entrees.Clear();
        await ChargerPageAsync();
    }

    [RelayCommand(CanExecute = nameof(PeutChargerPlus))]
    private async Task ChargerPlusAsync()
    {
        _page++;
        await ChargerPageAsync();
    }

    private async Task ChargerPageAsync()
    {
        var dateDebut = ParserDate(FiltreDateDebut, "Date de début");
        if (dateDebut.IsFailure)
        {
            await _dialogs.ShowErrorAsync(dateDebut.Error.Message);
            return;
        }

        var dateFin = ParserDate(FiltreDateFin, "Date de fin");
        if (dateFin.IsFailure)
        {
            await _dialogs.ShowErrorAsync(dateFin.Error.Message);
            return;
        }

        EnCours = true;
        try
        {
            var filtre = new FiltreAuditLog(
                Actor: NullSiVide(FiltreActeur),
                Action: NullSiVide(FiltreAction),
                EntityType: NullSiVide(FiltreTypeEntite),
                DateDebut: dateDebut.Value,
                DateFin: dateFin.Value,
                Page: _page);

            var result = await _listerAuditLog.ExecuterAsync(filtre);
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }

            foreach (var entree in result.Value)
                Entrees.Add(entree);

            PeutChargerPlus = result.Value.Count == FiltreAuditLog.TaillePageParDefaut;
        }
        finally
        {
            EnCours = false;
        }
    }

    private static string? NullSiVide(string? valeur)
        => string.IsNullOrWhiteSpace(valeur) ? null : valeur.Trim();

    private static Result<DateTimeOffset?> ParserDate(string valeur, string libelle)
    {
        if (string.IsNullOrWhiteSpace(valeur))
            return Result.Success<DateTimeOffset?>(null);

        return DateOnly.TryParseExact(valeur.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? Result.Success<DateTimeOffset?>(new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero))
            : Result.Failure<DateTimeOffset?>(
                Error.Validation($"{libelle} invalide (attendu yyyy-MM-dd) : « {valeur} »."));
    }
}
