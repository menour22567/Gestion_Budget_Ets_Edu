using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Presentation.Dialogs;
using PaieEducation.Presentation.Navigation;

namespace PaieEducation.Presentation.Workbench;

/// <summary>
/// Écran « Matrice de couverture » (Phase 6, tâche 9 ; pivotée P7, décisions
/// utilisateur du 20/07/2026) — pivote en <see cref="Lignes"/>/<see cref="Colonnes"/>
/// (une ligne par corps, une colonne par rubrique) la liste plate produite par
/// <see cref="ListerMatriceCouverture"/> (inchangée : le pivot et les filtres
/// vivent entièrement ici, le use case reste plat et testable). 3 états
/// (<see cref="EtatCouverture"/>), pas de 4e état « non applicable » (aucun
/// concept de portée en base pour le distinguer honnêtement d'un vrai trou).
/// Clic sur une cellule → <see cref="FicheRubriqueViewModel"/> préchargée
/// dans un nouvel onglet (drill-down, via <see cref="INavigationService.OpenTab{T}(string, Action{T})"/>).
/// </summary>
public sealed partial class MatriceCouvertureViewModel : ObservableObject
{
    private static readonly IReadOnlyList<string> EtatsFiltrablesSource = ["Tous", "Actives", "Inactives", "Non couvertes"];

    private readonly ListerMatriceCouverture _listerMatriceCouverture;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;

    private IReadOnlyList<CelluleCouverture> _cellulesBrutes = [];

    [ObservableProperty]
    private string datePaie = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");

    [ObservableProperty]
    private bool enCours;

    [ObservableProperty]
    private string filtreCorps = string.Empty;

    [ObservableProperty]
    private string filtreRubrique = string.Empty;

    [ObservableProperty]
    private string filtreEtat = "Tous";

    public IReadOnlyList<string> EtatsFiltrables => EtatsFiltrablesSource;

    public ObservableCollection<string> Colonnes { get; } = [];
    public ObservableCollection<LigneMatriceCorps> Lignes { get; } = [];

    public MatriceCouvertureViewModel(
        ListerMatriceCouverture listerMatriceCouverture, INavigationService navigation, IDialogService dialogs)
    {
        _listerMatriceCouverture = listerMatriceCouverture ?? throw new ArgumentNullException(nameof(listerMatriceCouverture));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    [RelayCommand]
    private async Task ChargerAsync()
    {
        EnCours = true;
        try
        {
            var result = await _listerMatriceCouverture.ExecuterAsync(new ListerMatriceCouverture.Demande(DatePaie));
            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync(result.Error.Message);
                return;
            }

            _cellulesBrutes = result.Value;
            RecalculerPivot();
        }
        finally
        {
            EnCours = false;
        }
    }

    [RelayCommand]
    private void NaviguerVersFiche(string rubriqueId)
    {
        var datePaieCourante = DatePaie;
        _navigation.OpenTab<FicheRubriqueViewModel>($"Fiche rubrique — {rubriqueId}", fiche =>
        {
            fiche.RubriqueId = rubriqueId;
            fiche.DatePaie = datePaieCourante;
            _ = fiche.ChargerCommand.ExecuteAsync(null);
        });
    }

    partial void OnFiltreCorpsChanged(string value) => RecalculerPivot();
    partial void OnFiltreRubriqueChanged(string value) => RecalculerPivot();
    partial void OnFiltreEtatChanged(string value) => RecalculerPivot();

    private void RecalculerPivot()
    {
        var corpsFiltres = _cellulesBrutes
            .Select(c => c.CorpsId)
            .Distinct(StringComparer.Ordinal)
            .Where(id => string.IsNullOrWhiteSpace(FiltreCorps) || id.Contains(FiltreCorps, StringComparison.OrdinalIgnoreCase))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var rubriquesFiltrees = _cellulesBrutes
            .Select(c => c.RubriqueId)
            .Distinct(StringComparer.Ordinal)
            .Where(id => string.IsNullOrWhiteSpace(FiltreRubrique) || id.Contains(FiltreRubrique, StringComparison.OrdinalIgnoreCase))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var cellulesParCorps = _cellulesBrutes.ToLookup(c => c.CorpsId, StringComparer.Ordinal);

        var lignes = new List<LigneMatriceCorps>();
        foreach (var corpsId in corpsFiltres)
        {
            var etats = new Dictionary<string, EtatCouverture>(StringComparer.Ordinal);
            foreach (var rubriqueId in rubriquesFiltrees)
            {
                var cellule = cellulesParCorps[corpsId].FirstOrDefault(c => c.RubriqueId == rubriqueId);
                etats[rubriqueId] = cellule is null
                    ? EtatCouverture.NonCouverte
                    : EtatCouvertureClassificateur.Classifier(cellule.Couverte, cellule.Active);
            }

            if (FiltreEtat != "Tous" && !etats.Values.Any(e => CorrespondAuFiltreEtat(e, FiltreEtat)))
                continue;

            lignes.Add(new LigneMatriceCorps(corpsId, etats));
        }

        Colonnes.Clear();
        foreach (var rubriqueId in rubriquesFiltrees)
            Colonnes.Add(rubriqueId);

        Lignes.Clear();
        foreach (var ligne in lignes)
            Lignes.Add(ligne);
    }

    private static bool CorrespondAuFiltreEtat(EtatCouverture etat, string filtre) => filtre switch
    {
        "Actives" => etat == EtatCouverture.Active,
        "Inactives" => etat == EtatCouverture.Inactive,
        "Non couvertes" => etat == EtatCouverture.NonCouverte,
        _ => true,
    };
}
