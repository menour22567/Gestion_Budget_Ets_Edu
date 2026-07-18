using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Presentation.Dialogs;

namespace PaieEducation.Presentation.Workbench;

/// <summary>
/// Écran « Audit &amp; traçabilité » (Phase 6, tâche 4) — les 500 entrées
/// les plus récentes du journal d'audit (<see cref="ListerAuditLog"/>,
/// déjà livré). Chargé au montage (comme les sélecteurs référentiels,
/// <see cref="ChargerCommand"/> invoqué en fire-and-forget dans le
/// constructeur) — pas de filtre de recherche explicite (même patron
/// assumé que la matrice de couverture : <c>DataGrid</c> à colonnes
/// statiques, tri natif par en-tête).
/// </summary>
public sealed partial class AuditLogViewModel : ObservableObject
{
    private readonly ListerAuditLog _listerAuditLog;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private bool enCours;

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
        EnCours = true;
        try
        {
            var result = await _listerAuditLog.ExecuterAsync();
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }

            Entrees.Clear();
            foreach (var entree in result.Value)
                Entrees.Add(entree);
        }
        finally
        {
            EnCours = false;
        }
    }
}
