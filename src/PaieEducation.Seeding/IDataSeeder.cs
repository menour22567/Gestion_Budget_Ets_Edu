namespace PaieEducation.Seeding;

/// <summary>
/// Port de remplissage initial de la base (référentiels réglementaires).
/// Implémenté par <see cref="DatabaseSeeder"/>, invocable aussi bien par la
/// CLI <c>Tools</c> que par le <c>Bootstrapper</c> (auto-seed au 1er
/// lancement, C1). Le contrat est volontairement indépendant de tout projet
/// console : il prend une connexion déjà ouverte et idempotente.
/// </summary>
public interface IDataSeeder
{
    /// <summary>
    /// Remplit l'intégralité du référentiel (nomenclature, réglementaire, IRG,
    /// formules). Idempotent : ré-exécuté sur une base déjà peuplée, il ne
    /// duplique aucune ligne (<c>INSERT ... ON CONFLICT DO NOTHING</c>).
    /// </summary>
    /// <param name="connection">Connexion SQLite déjà ouverte (PRAGMA
    ///     foreign_keys=ON recommandé).</param>
    /// <param name="ct">Jeton d'annulation.</param>
    /// <returns>Rapport agrégé par table touchée.</returns>
    Task<SeedReport> SeedAllAsync(Microsoft.Data.Sqlite.SqliteConnection connection, CancellationToken ct = default);
}
