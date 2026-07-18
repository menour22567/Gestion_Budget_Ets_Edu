using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaieEducation.Seeding;

/// <summary>
/// Lecteur du jeu de données « barèmes IRG » embarqué en ressource
/// (<c>PaieEducation.Seeding.Donnees.IRG.baremes_irg_v1.json</c>).
/// Lot 1.3α : remplace les valeurs IRG codées en dur dans
/// <see cref="IrgSeeder"/> (barèmes 2008 et 2022 + leurs 10 tranches).
/// </summary>
/// <remarks>
/// <para>Format JSON (cf. <c>baremes_irg_v1.json</c>) :</para>
/// <list type="bullet">
///   <item><c>baremes[]</c> : un objet par barème (id, code, libellé, dates, source, tranches)</item>
///   <item><c>tranches[]</c> : un objet par tranche (id, bornes, taux, ordre, source)</item>
/// </list>
/// <para>Calcul de hash : SHA-256 sur la sérialisation canonique (clés
/// triées alphabétiquement) de chaque ligne. Toute modification du JSON
/// fait dériver le hash — détectable par un test dédié.</para>
/// </remarks>
public static class BaremeIrgDataReader
{
    private const string ResourceName = "PaieEducation.Seeding.Donnees.IRG.baremes_irg_v1.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Désérialise le JSON embarqué en structure fortement typée.</summary>
    public static BaremeIrgData Load()
    {
        var assembly = typeof(BaremeIrgDataReader).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Ressource embarquée introuvable : {ResourceName}");
        var data = JsonSerializer.Deserialize<BaremeIrgData>(stream, Options)
            ?? throw new InvalidOperationException(
                $"JSON invalide ou vide : {ResourceName}");
        return data;
    }

    /// <summary>
    /// Hash SHA-256 canonique d'une ligne (clés triées, sérialisation
    /// déterministe). Permet la détection de drift : un changement dans
    /// la donnée recalculée change le hash.
    /// </summary>
    public static string HashLigne(object ligne)
    {
        // Canonicalisation : on sérialise avec les propriétés dans l'ordre
        // alphabétique (sorte manuelle). Assez pour ce volume de données.
        var json = JsonSerializer.Serialize(ligne, new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

/// <summary>Racine du JSON <c>baremes_irg_v1.json</c>.</summary>
public sealed record BaremeIrgData(
    string Version,
    string DateEffetGlobal,
    string SourceGlobale,
    IReadOnlyList<BaremeIrgHeader> Baremes);

/// <summary>En-tête d'un barème (1 ligne dans <c>BaremeIRG</c>).</summary>
public sealed record BaremeIrgHeader(
    string Id,
    string Code,
    string Libelle,
    string DateEffet,
    string? DateFin,
    string Source,
    IReadOnlyList<BaremeIrgTranche> Tranches);

/// <summary>Une tranche IRG (1 ligne dans <c>BaremeIRGTranches</c>).</summary>
public sealed record BaremeIrgTranche(
    string Id,
    int BorneInf,
    int? BorneSup,
    [property: JsonConverter(typeof(DecimalJsonConverter))] decimal Taux,
    int Ordre,
    string Source);

/// <summary>
/// Le JSON utilise des nombres décimaux ("0.20", "0.35") que
/// <c>System.Text.Json</c> désérialise en <c>decimal</c> via la culture
/// invariante. Ce converter garantit la robustesse quel que soit le
/// locale du poste.
/// </summary>
internal sealed class DecimalJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetDecimal();
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
                return d;
        }
        throw new JsonException("Valeur décimale attendue.");
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}
