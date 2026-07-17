using PaieEducation.Domain.Common;

namespace PaieEducation.Domain.Calcul.Repositories;

/// <summary>
/// Écrit de nouvelles versions de la grille indiciaire (<c>ValeurPoint</c>,
/// <c>GrilleIndiciaire</c>, <c>IndicesEchelon</c>, V003) — symétrique en
/// écriture de <see cref="IVariableRepository"/>, qui les lit. Port du Domain
/// implémenté par <c>Infrastructure.Repositories.Payroll.GrilleIndiciaireRepository</c>.
/// </summary>
/// <remarks>
/// Les 3 méthodes suivent le même invariant : une nouvelle version ferme la
/// précédente (jamais de recouvrement, jamais de réécriture rétroactive
/// silencieuse) — échec explicite (<see cref="Error.Conflict"/> si la date
/// d'effet existe déjà, <see cref="Error.Validation"/> si elle n'est pas
/// postérieure à la version en vigueur ou si la valeur viole une borne
/// métier), jamais une exception SQLite qui fuit.
/// </remarks>
public interface IGrilleIndiciaireRepository
{
    /// <summary>Définit une nouvelle valeur du point indiciaire à compter de <paramref name="dateEffet"/>.</summary>
    /// <returns>L'Id (code métier) de la ligne créée.</returns>
    Task<Result<string>> DefinirValeurPointAsync(
        decimal valeur, string dateEffet, string version, string? source, DateTimeOffset creeLe, CancellationToken ct = default);

    /// <summary>Définit un nouvel indice minimum de grille pour une catégorie à compter de <paramref name="dateEffet"/>.</summary>
    /// <returns>L'Id (code métier) de la ligne créée.</returns>
    Task<Result<string>> DefinirIndiceMinAsync(
        string categorieId, int indiceMin, string dateEffet, string version, string? source, DateTimeOffset creeLe,
        CancellationToken ct = default);

    /// <summary>Définit un nouvel indice d'échelon à compter de <paramref name="dateEffet"/>.</summary>
    /// <returns>L'Id (code métier) de la ligne créée.</returns>
    Task<Result<string>> DefinirIndiceEchelonAsync(
        string echelonId, int indice, string dateEffet, string version, string? source, DateTimeOffset creeLe,
        CancellationToken ct = default);

    /// <summary>
    /// Clone la valeur du point indiciaire en vigueur vers une nouvelle version
    /// (même <c>Valeur</c>, nouvelle <paramref name="nouvelleDateEffet"/>/<paramref name="version"/>/<paramref name="source"/>) —
    /// mode « Duplication » (J3I §7.4, reconduction d'un taux sur une nouvelle
    /// période réglementaire). Échec explicite (<see cref="Error.NotFound"/>)
    /// si aucune version n'est en vigueur à cloner.
    /// </summary>
    /// <returns>L'Id (code métier) de la ligne créée.</returns>
    Task<Result<string>> DupliquerValeurPointAsync(
        string nouvelleDateEffet, string version, string? source, DateTimeOffset creeLe, CancellationToken ct = default);
}
