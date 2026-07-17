using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Application.Agents.UseCases;
using PaieEducation.Application.Referentiels.UseCases;
using PaieEducation.Domain.Agents;
using PaieEducation.Domain.Calcul.ValueObjects;
using PaieEducation.Presentation.Dialogs;

namespace PaieEducation.Presentation.Agents;

/// <summary>
/// Écran « Créer un agent » (Phase 6, tâche 3 — gestion agents/carrière),
/// appelle le use case <see cref="CreerAgent"/> (déjà livré, Phase 5).
/// </summary>
/// <remarks>
/// <c>GradeId</c>/<c>CategorieId</c>/<c>EchelonId</c> sont sélectionnés via
/// <see cref="ListerReferentiels"/> (chargé au montage de l'écran, cf.
/// <see cref="ChargerReferentielsCommand"/>) plutôt que saisis en texte
/// brut. <c>Sexe</c>/<c>SituationFamiliale</c>/<c>TypeContrat</c> restent
/// des listes fermées (mêmes valeurs que les contraintes <c>CHECK</c>
/// V011) : un <c>ComboBox</c> statique suffit, pas besoin d'une source de
/// données.
/// </remarks>
public sealed partial class CreerAgentViewModel : ObservableObject
{
    public IReadOnlyList<string> SexesDisponibles { get; } = ["M", "F"];
    public IReadOnlyList<string> SituationsDisponibles { get; } = ["CELIBATAIRE", "MARIE", "DIVORCE", "VEUF"];
    public IReadOnlyList<string> TypesContratDisponibles { get; } = ["STATUTAIRE", "CONTRACTUEL"];

    public ObservableCollection<ReferentielItem> GradesDisponibles { get; } = [];
    public ObservableCollection<ReferentielItem> CategoriesDisponibles { get; } = [];
    public ObservableCollection<ReferentielItem> EchelonsDisponibles { get; } = [];

    private readonly CreerAgent _creerAgent;
    private readonly ListerReferentiels _listerReferentiels;
    private readonly IDialogService _dialogs;

    [ObservableProperty] private string matricule = string.Empty;
    [ObservableProperty] private string nom = string.Empty;
    [ObservableProperty] private string prenom = string.Empty;
    [ObservableProperty] private string dateNaissance = string.Empty;
    [ObservableProperty] private string dateRecrutement = string.Empty;
    [ObservableProperty] private string sexe = "M";
    [ObservableProperty] private string situationFamiliale = "CELIBATAIRE";
    [ObservableProperty] private ReferentielItem? gradeSelectionne;
    [ObservableProperty] private ReferentielItem? categorieSelectionnee;
    [ObservableProperty] private ReferentielItem? echelonSelectionne;
    [ObservableProperty] private string typeContrat = "STATUTAIRE";
    [ObservableProperty] private string? resultat;
    [ObservableProperty] private bool enCours;

    public CreerAgentViewModel(CreerAgent creerAgent, ListerReferentiels listerReferentiels, IDialogService dialogs)
    {
        _creerAgent = creerAgent ?? throw new ArgumentNullException(nameof(creerAgent));
        _listerReferentiels = listerReferentiels ?? throw new ArgumentNullException(nameof(listerReferentiels));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        _ = ChargerReferentielsCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task ChargerReferentielsAsync()
    {
        var result = await _listerReferentiels.ExecuterAsync();
        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync(result.Error.Message);
            return;
        }

        GradesDisponibles.Clear();
        foreach (var g in result.Value.Grades) GradesDisponibles.Add(g);
        CategoriesDisponibles.Clear();
        foreach (var c in result.Value.Categories) CategoriesDisponibles.Add(c);
        EchelonsDisponibles.Clear();
        foreach (var e in result.Value.Echelons) EchelonsDisponibles.Add(e);
    }

    [RelayCommand]
    private async Task CreerAsync()
    {
        EnCours = true;
        Resultat = null;
        try
        {
            var demande = new NouvelAgent(
                Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, SituationFamiliale,
                GradeSelectionne?.Id ?? string.Empty, CategorieSelectionnee?.Id ?? string.Empty,
                EchelonSelectionne?.Id ?? string.Empty, TypeContrat);
            var result = await _creerAgent.ExecuterAsync(demande);
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }

            Resultat = $"Agent créé : {result.Value}";
        }
        finally
        {
            EnCours = false;
        }
    }
}
