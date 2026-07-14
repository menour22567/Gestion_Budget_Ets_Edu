namespace PaieEducation.Persistence.Migrations;

/// <summary>
/// Configuration du <see cref="SqliteMigrator"/>.
/// </summary>
/// <param name="ConnectionString">
/// Chaîne de connexion SQLite (ex. <c>Data Source=paie.db</c> ou
/// <c>Data Source=:memory:</c> pour les tests). Doit être ouvrable en
/// lecture/écriture (le migrateur crée la base si elle n'existe pas).
/// </param>
/// <param name="AppliedBy">
/// Identifiant de l'acteur enregistré dans <c>SchemaVersions.AppliedBy</c>
/// (utilisateur OS, nom du job d'import, « test »...). Vaut <c>« system »</c> par défaut.
/// </param>
public sealed record SqliteMigratorOptions(
    string ConnectionString,
    string AppliedBy = "system");
