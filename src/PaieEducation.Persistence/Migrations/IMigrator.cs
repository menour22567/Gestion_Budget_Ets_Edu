using PaieEducation.Shared.Results;

namespace PaieEducation.Persistence.Migrations;

/// <summary>
/// Service de migration du schéma SQLite. Toutes les implémentations doivent
/// garantir l'idempotence (réexécutable sans casse) et l'atomicité par migration.
/// </summary>
public interface IMigrator
{
    /// <summary>
    /// Applique toutes les migrations en attente dans l'ordre des versions croissantes.
    /// Chaque migration est exécutée dans sa propre transaction. Les PRAGMA
    /// <c>foreign_keys=ON</c> et <c>journal_mode=WAL</c> (sur DB fichier) sont positionnés
    /// à l'ouverture de la connexion.
    /// </summary>
    /// <returns>
    /// Un <see cref="Result{T}"/> contenant le nombre de migrations effectivement appliquées
    /// (0 si la base est déjà à jour), ou une <see cref="Error"/> en cas d'échec.
    /// </returns>
    Result<int> Apply();

    /// <summary>
    /// Lit la version actuelle du schéma (PRAGMA <c>user_version</c>). Retourne 0
    /// sur une base vierge (avant toute migration).
    /// </summary>
    Result<int> GetCurrentVersion();
}
