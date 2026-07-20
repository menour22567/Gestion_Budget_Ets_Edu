using PaieEducation.Domain.Common;
using PaieEducation.Shared.Results;

namespace PaieEducation.Domain.Workbench.Repositories;

/// <summary>
/// Écrit de nouvelles versions de barème (<c>RubriqueBaremes</c>, V008 §
/// 8bis.1) — symétrique en écriture de
/// <see cref="IWorkbenchReadRepository.ListerBaremesRubriqueAsync"/>. Port du
/// Domain implémenté par
/// <c>Infrastructure.Repositories.Workbench.RubriqueBaremeRepository</c>.
/// Chantier P5 (audit du 19/07/2026) — premier chemin d'écriture pour cette
/// table (jusqu'ici seedée uniquement, cf. <c>ConsulterFicheRubrique</c> :
/// « aucun chemin d'écriture pour les barèmes... n'existe encore »).
/// </summary>
/// <remarks>
/// Même invariant que <c>IGrilleIndiciaireRepository</c> (ADR-0008,
/// immutabilité des périodes clôturées) : une nouvelle version d'une tranche
/// ferme la précédente (jamais de recouvrement, jamais de réécriture
/// rétroactive silencieuse) — échec explicite (<see cref="Error.Conflict"/>
/// si la date d'effet existe déjà pour la même tranche,
/// <see cref="Error.Validation"/> si elle n'est pas postérieure à la version
/// en vigueur, <see cref="Error.NotFound"/> si la rubrique référencée
/// n'existe pas), jamais une exception SQLite (FK, CHECK) qui fuit.
/// L'identité d'une tranche est <c>(RubriqueId, Dimension, BorneInf)</c> —
/// l'index unique <c>IX_RubriqueBaremes_Rub_Dim_Borne_Date</c> l'étend avec
/// <c>DateEffet</c> pour le versionnement ; <c>BorneSup</c>/<c>TypeValeur</c>/<c>Valeur</c>
/// peuvent varier d'une version à l'autre de la même tranche.
/// </remarks>
public interface IRubriqueBaremeRepository
{
    /// <summary>
    /// Définit une nouvelle valeur de barème pour une tranche
    /// <c>(rubriqueId, dimension, borneInf)</c> à compter de
    /// <paramref name="dateEffet"/>. Ferme la version en vigueur de la même
    /// tranche si elle existe (<c>DateFin</c> = veille de <paramref name="dateEffet"/>).
    /// </summary>
    /// <returns>L'Id (code métier) de la ligne créée.</returns>
    Task<Result<string>> DefinirValeurBaremeAsync(
        string rubriqueId,
        string dimension,
        string borneInf,
        string? borneSup,
        string typeValeur,
        string valeur,
        string dateEffet,
        string? source,
        DateTimeOffset creeLe,
        CancellationToken ct = default,
        IUnitOfWork? uow = null);
}
