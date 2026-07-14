namespace PaieEducation.Persistence.Migrations;

/// <summary>
/// Représente une migration de schéma versionnée : numéro (croissant), nom descriptif
/// (utilisé dans <c>SchemaVersions.Name</c> et dans les logs), et le script SQL
/// à appliquer dans une transaction.
/// </summary>
/// <remarks>
/// Le <c>Checksum</c> (SHA-256 du SQL normalisé) est calculé à l'application et stocké
/// dans <c>SchemaVersions.Checksum</c>. Il permet de détecter un drift si une migration
/// déjà appliquée est modifiée ultérieurement (à coupler avec un test d'intégrité).
/// </remarks>
public sealed record Migration(int Version, string Name, string Sql);
