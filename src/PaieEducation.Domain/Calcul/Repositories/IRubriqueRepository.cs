using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Domain.Calcul.Repositories;

/// <summary>
/// Écriture du référentiel des rubriques (C4.1, Workbench d'édition) :
/// création/édition d'une rubrique, de sa formule versionnée et de ses
/// paramètres versionnés. Symétrique en écriture des lectures du moteur.
/// Port du Domain implémenté par
/// <c>Infrastructure.Repositories.Payroll.RubriqueRepository</c>.
/// </summary>
/// <remarks>
/// Invariants communs : une nouvelle version ferme la précédente (jamais de
/// recouvrement, jamais de réécriture rétroactive silencieuse) — échec explicite
/// (<see cref="Error.Conflict"/> si la date d'effet existe déjà,
/// <see cref="Error.Validation"/> si elle n'est pas postérieure à la version en
/// vigueur), jamais une exception SQLite qui fuit. La formule est validée par
/// le <see cref="FormulaParser"/> avant persistance ; l'ajout d'une dépendance
/// refuse un cycle dans le graphe DAG.
/// </remarks>
public interface IRubriqueRepository
{
    /// <summary>Crée (ou met à jour les métadonnées de) une rubrique.</summary>
    /// <returns>L'Id (code métier) de la rubrique.</returns>
    Task<Result<string>> DefinirRubriqueAsync(
        string id, string libelle, string nature, string baseCalcul, string periodicite,
        string? periodiciteVersement, int ordreCalcul, bool estImposable, bool estCotisable,
        string description, bool estAffectableManuellement, bool occurrencesMultiples,
        string? sourceValeurId, string? source, DateTimeOffset creeLe, CancellationToken ct = default);

    /// <summary>Définit une nouvelle version de formule pour une rubrique.</summary>
    /// <returns>L'Id (code métier) de la formule créée.</returns>
    Task<Result<string>> DefinirFormuleAsync(
        string rubriqueId, string expression, string dateEffet, int ordre,
        string? source, DateTimeOffset creeLe, CancellationToken ct = default);

    /// <summary>Définit un nouveau paramètre versionné pour une rubrique.</summary>
    /// <returns>L'Id (code métier) du paramètre créé.</returns>
    Task<Result<string>> DefinirParametreAsync(
        string rubriqueId, string cle, string valeur, string dateEffet,
        string? source, DateTimeOffset creeLe, CancellationToken ct = default);

    /// <summary>
    /// Déclare une dépendance (la rubrique <paramref name="rubriqueId"/> dépend de
    /// <paramref name="dependDeId"/> pour son calcul). Refuse un cycle
    /// (<see cref="Error.Cycle"/>) et une auto-dépendance.
    /// </returns>
    Task<Result<string>> DefinirDependanceAsync(
        string rubriqueId, string dependDeId, string dateEffet,
        string? source, DateTimeOffset creeLe, CancellationToken ct = default);
}
