using System.Reflection;
using System.Text.Json;

namespace PaieEducation.Seeding;

/// <summary>
/// Lecteur du jeu de données « formules de calcul » embarqué en ressource
/// (<c>PaieEducation.Seeding.Donnees.Formules.formules_v1.json</c>).
/// Lot 1.3 finalisation : remplace les valeurs codees en dur dans
/// <see cref="FormulesSeeder"/> (1 rubrique TRAITEMENT + 6 formules).
/// </summary>
/// <remarks>
/// Meme pattern que <see cref="BaremeIrgDataReader"/> (Lot 1.3 alpha) :
/// JSON embarque en ressource via <c>EmbeddedResource</c> dans le csproj,
/// hash SHA-256 sur la serialisation canonique pour detecter le drift.
/// </remarks>
public static class FormulesJsonDataReader
{
    private const string ResourceName = "PaieEducation.Seeding.Donnees.Formules.formules_v1.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Deserialise le JSON embarque en structure fortement typee.</summary>
    public static FormulesData Load()
    {
        var assembly = typeof(FormulesJsonDataReader).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Ressource embarquee introuvable : {ResourceName}");
        var data = JsonSerializer.Deserialize<FormulesData>(stream, Options)
            ?? throw new InvalidOperationException(
                $"JSON invalide ou vide : {ResourceName}");
        return data;
    }

    /// <summary>
    /// Hash SHA-256 canonique d'une ligne (meme contrat que
    /// <see cref="BaremeIrgDataReader.HashLigne"/>). Detecte tout drift
    /// entre le JSON et la base.
    /// </summary>
    public static string HashLigne(object ligne)
        => BaremeIrgDataReader.HashLigne(ligne);
}

/// <summary>Racine du JSON <c>formules_v1.json</c>.</summary>
public sealed record FormulesData(
    string Version,
    string DateEffet,
    string SourceGlobale,
    RubriqueTraitementSeed RubriqueTraitement,
    IReadOnlyList<FormuleSeed> Formules);

/// <summary>En-tete de la rubrique TRAITEMENT (1 ligne dans <c>Rubriques</c>).</summary>
public sealed record RubriqueTraitementSeed(
    string Id,
    string Libelle,
    string Description,
    string Source);

/// <summary>Une formule (1 ligne dans <c>RubriqueFormules</c>).</summary>
public sealed record FormuleSeed(
    string RubriqueId,
    string Expression,
    string Source);
