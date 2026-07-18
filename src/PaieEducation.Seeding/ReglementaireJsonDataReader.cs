using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaieEducation.Seeding;

/// <summary>
/// Lecteur du référentiel réglementaire embarqué en ressource
/// (<c>PaieEducation.Seeding.Donnees.Reglementaire.referentiel_reglementaire_v1.json</c>).
/// Lot 1.3 finalisation : remplace les 10 rubriques + 5 barèmes + 3
/// cotisations + 10 paramètres codés en dur dans
/// <see cref="ReglementaireSeeder"/>.
/// </summary>
/// <remarks>
/// Mêmes garanties que <see cref="BaremeIrgDataReader"/> et
/// <see cref="FormulesJsonDataReader"/> : JSON embarqué via
/// <c>EmbeddedResource</c>, hash SHA-256 canonique pour détecter le
/// drift. Les groupes DNF ISSRP et les 4 grades hors catégorie
/// (Q-C3) restent en C# dans le seeder : leur volume (~92 grade IDs
/// dans des listes IN) et leur couplage avec le reste du code
/// d'éligibilité demandent un format de fichier dédié (lot suivant).
/// </remarks>
public static class ReglementaireJsonDataReader
{
    private const string ResourceName =
        "PaieEducation.Seeding.Donnees.Reglementaire.referentiel_reglementaire_v1.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Désérialise le JSON embarqué en structure fortement typée.</summary>
    public static ReferentielReglementaireData Load()
    {
        var assembly = typeof(ReglementaireJsonDataReader).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Ressource embarquée introuvable : {ResourceName}");
        var data = JsonSerializer.Deserialize<ReferentielReglementaireData>(stream, Options)
            ?? throw new InvalidOperationException(
                $"JSON invalide ou vide : {ResourceName}");
        return data;
    }

    /// <summary>Hash SHA-256 canonique d'une ligne (délègue à BaremeIrgDataReader).</summary>
    public static string HashLigne(object ligne)
        => BaremeIrgDataReader.HashLigne(ligne);
}

/// <summary>Racine du JSON <c>referentiel_reglementaire_v1.json</c>.</summary>
public sealed record ReferentielReglementaireData(
    string Version,
    string SourceGlobale,
    IReadOnlyList<RubriqueReglementaireSeed> Rubriques,
    IReadOnlyList<BaremeReglementaireSeed> Baremes,
    IReadOnlyList<CotisationReglementaireSeed> Cotisations,
    IReadOnlyList<ParametreReglementaireSeed> Parametres);

/// <summary>Une rubrique (1 ligne dans <c>Rubriques</c>).</summary>
public sealed record RubriqueReglementaireSeed(
    string Id,
    string Libelle,
    string Nature,
    string BaseCalcul,
    string Periodicite,
    string? PeriodiciteVersement,
    int OrdreCalcul,
    bool EstImposable,
    bool EstCotisable,
    string Description,
    bool EstAffectableManuellement,
    bool OccurrencesMultiples);

/// <summary>Un barème (1 ligne dans <c>RubriqueBaremes</c>).</summary>
public sealed record BaremeReglementaireSeed(
    string Id,
    string RubriqueId,
    string Dimension,
    string BorneInf,
    string? BorneSup,
    string TypeValeur,
    string Valeur,
    string DateEffet,
    string Source);

/// <summary>Une cotisation (1 ligne dans <c>Cotisations</c>).</summary>
public sealed record CotisationReglementaireSeed(
    string Id,
    string Code,
    string Libelle,
    string Type,
    [property: JsonConverter(typeof(NullableDoubleJsonConverter))] double? Taux,
    string AssietteRef,
    bool EstRetenue,
    string DateEffet,
    string Source);

/// <summary>Un paramètre (1 ligne dans <c>Parametres</c>).</summary>
public sealed record ParametreReglementaireSeed(
    string Id,
    string Cle,
    string Valeur,
    string Type,
    string Description,
    string DateEffet);

/// <summary>
/// Le JSON autorise <c>"taux": null</c> pour les cotisations facultatives
/// (Mutuelle, Œuvres sociales) à montant fixe. Ce converter garantit la
/// désérialisation en <c>double?</c> quel que soit le format.
/// </summary>
internal sealed class NullableDoubleJsonConverter : JsonConverter<double?>
{
    public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType == JsonTokenType.Number) return reader.GetDouble();
        if (reader.TokenType == JsonTokenType.String
            && double.TryParse(reader.GetString(),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var d))
            return d;
        throw new JsonException("Valeur décimale ou null attendue.");
    }

    public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value ?? 0);
}
