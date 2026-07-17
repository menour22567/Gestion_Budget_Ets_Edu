using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Domain.Workbench.Repositories;

/// <summary>
/// Sous-ensemble minimal de lecture du Workbench réglementaire nécessaire à
/// <c>SuggererRubriques</c> — pas un miroir complet de
/// <c>Infrastructure.Repositories.Workbench.WorkbenchReadRepository</c> (qui
/// expose davantage de méthodes pour d'autres besoins non encore portés en
/// Application). Port du Domain implémenté par cette même classe.
/// </summary>
public interface IWorkbenchReadRepository
{
    /// <summary>Ids des rubriques actives et affectables manuellement (<c>EstAffectableManuellement = 1</c>) à la date donnée.</summary>
    Task<IReadOnlyList<string>> ListerRubriquesAffectablesAsync(string datePaie, CancellationToken ct = default);

    /// <summary>Toutes les conditions d'éligibilité d'une rubrique, actives à la date donnée.</summary>
    Task<IReadOnlyList<ConditionEligibilite>> ListerConditionsParRubriqueAsync(
        string rubriqueId, string datePaie, CancellationToken ct = default);

    /// <summary>Dictionnaire des critères d'éligibilité actifs, indexé par Id.</summary>
    Task<IReadOnlyDictionary<string, CritereEligibilite>> ListerCriteresParIdAsync(CancellationToken ct = default);

    /// <summary>En-têtes des groupes DNF d'une rubrique, actifs à la date donnée.</summary>
    Task<IReadOnlyList<GroupeEligibilite>> ListerGroupesParRubriqueAsync(
        string rubriqueId, string datePaie, CancellationToken ct = default);

    /// <summary>Corps actifs (nomenclature) — pour la matrice de couverture (D11).</summary>
    Task<IReadOnlyList<CorpsResume>> ListerCorpsActifsAsync(CancellationToken ct = default);

    /// <summary>Rubriques actives (nomenclature) — pour la matrice de couverture (D11).</summary>
    Task<IReadOnlyList<RubriqueResume>> ListerRubriquesActivesAsync(CancellationToken ct = default);

    /// <summary>Grades actifs avec leur corps d'appartenance — pour résoudre les conditions <c>GRADE</c> vers un corps (D11).</summary>
    Task<IReadOnlyList<(string GradeId, string CorpsId)>> ListerGradesActifsAsync(CancellationToken ct = default);

    /// <summary>
    /// Toutes les conditions d'éligibilité <c>CritereId ∈ {GRADE, CORPS}</c>,
    /// **actives et expirées** (contrairement aux autres méthodes de lecture,
    /// pas de filtre par date — la matrice de couverture (D11) doit distinguer
    /// une règle active d'une règle expirée, pas seulement l'état courant).
    /// </summary>
    Task<IReadOnlyList<ConditionEligibilite>> ListerConditionsCorpsGradeAsync(CancellationToken ct = default);
}
