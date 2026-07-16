using System.Collections.Concurrent;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Infrastructure.Repositories.Workbench;

/// <summary>
/// Décorateur de cache mémoire du <see cref="WorkbenchReadRepository"/>
/// (J4.e § 7.4, lot 2-restes). Cache par (rubrique, date de paie) pour les
/// conditions et les groupes — « règles actives résolues par période » —,
/// global pour le dictionnaire de critères.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Invalider"/> DOIT être appelé par tout chemin d'écriture du
/// paramétrage (écrans Workbench Phase 6, seeders CLI) : il n'existe aucun
/// autre mécanisme d'expiration — l'application est hors ligne et
/// mono-utilisateur, la base ne change pas sous nos pieds.
/// </para>
/// <para>
/// Un échec de chargement n'est jamais mis en cache (l'entrée fautive est
/// retirée, l'exception propagée). Le <c>CancellationToken</c> du premier
/// appelant est celui du chargement partagé ; les appels suivants reçoivent
/// la valeur mémorisée.
/// </para>
/// </remarks>
public sealed class WorkbenchReadCache
{
    private readonly WorkbenchReadRepository _inner;
    private volatile ConcurrentDictionary<string, Lazy<Task<object>>> _entrees =
        new(StringComparer.Ordinal);

    public WorkbenchReadCache(WorkbenchReadRepository inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <summary>Dictionnaire des critères actifs (clé de cache globale).</summary>
    public Task<IReadOnlyDictionary<string, CritereEligibilite>> ListerCriteresParIdAsync(
        CancellationToken ct = default)
        => ObtenirAsync<IReadOnlyDictionary<string, CritereEligibilite>>(
            "criteres", () => _inner.ListerCriteresParIdAsync(ct));

    /// <summary>Conditions actives d'une rubrique à une date (clé rubrique × date).</summary>
    public Task<IReadOnlyList<ConditionEligibilite>> ListerConditionsParRubriqueAsync(
        string rubriqueId, string datePaie, CancellationToken ct = default)
        => ObtenirAsync<IReadOnlyList<ConditionEligibilite>>(
            $"conditions|{rubriqueId}|{datePaie}",
            () => _inner.ListerConditionsParRubriqueAsync(rubriqueId, datePaie, ct));

    /// <summary>En-têtes de groupes actifs d'une rubrique à une date (clé rubrique × date).</summary>
    public Task<IReadOnlyList<GroupeEligibilite>> ListerGroupesParRubriqueAsync(
        string rubriqueId, string datePaie, CancellationToken ct = default)
        => ObtenirAsync<IReadOnlyList<GroupeEligibilite>>(
            $"groupes|{rubriqueId}|{datePaie}",
            () => _inner.ListerGroupesParRubriqueAsync(rubriqueId, datePaie, ct));

    /// <summary>Messages actifs à une date (clé date).</summary>
    public Task<IReadOnlyList<MessageRegle>> ListerMessagesReglesActifsAsync(
        string datePaie, CancellationToken ct = default)
        => ObtenirAsync<IReadOnlyList<MessageRegle>>(
            $"messages|{datePaie}",
            () => _inner.ListerMessagesReglesActifsAsync(datePaie, ct));

    /// <summary>
    /// Vide intégralement le cache. À appeler après TOUTE écriture de
    /// paramétrage (rubriques, critères, conditions, groupes, messages,
    /// barèmes) — le prochain accès rechargera depuis la base.
    /// </summary>
    public void Invalider()
        => _entrees = new ConcurrentDictionary<string, Lazy<Task<object>>>(StringComparer.Ordinal);

    private async Task<T> ObtenirAsync<T>(string cle, Func<Task<T>> charger)
        where T : class
    {
        var entrees = _entrees;
        var lazy = entrees.GetOrAdd(cle, _ => new Lazy<Task<object>>(
            async () => await charger().ConfigureAwait(false),
            LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return (T)await lazy.Value.ConfigureAwait(false);
        }
        catch
        {
            // Jamais de mise en cache d'un échec : on retire l'entrée fautive
            // pour que l'appel suivant retente le chargement.
            entrees.TryRemove(cle, out _);
            throw;
        }
    }
}
