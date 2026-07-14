using System.Globalization;
using System.Text;
using PaieEducation.Tools.Seeding.Models;

namespace PaieEducation.Tools.Seeding;

/// <summary>
/// Parseur du CSV <c>Cascade_Corps_Grades_*.csv</c> (fourni dans
/// <c>Reglementation/</c>). Format :
/// <list type="bullet">
///   <item>Séparateur : <c>;</c></item>
///   <item>Encodage : Windows-1252 (CP1252) — important pour les accents (<c>é</c>, <c>è</c>, <c>à</c>)</item>
///   <item>En-tête multi-ligne : les noms des 4 colonnes <c>Indice Minimum *</c>
///         sont entre guillemets et contiennent des retours chariot. On le saute
///         en repérant la première ligne qui commence par un chiffre.</item>
///   <item>Lignes de données : 12 champs, premier champ = <c>Num_Ord</c> (entier).</item>
/// </list>
/// </summary>
/// <remarks>
/// Le parseur est **pur** (pas d'accès disque en dehors du <see cref="TextReader"/>
/// passé) et **testable** (les tests injectent un CSV en mémoire via
/// <see cref="StringReader"/>).
/// </remarks>
public sealed class CsvCascadeParser
{
    /// <summary>Nombre de colonnes attendues par ligne de données.</summary>
    public const int ExpectedColumnCount = 12;

    /// <summary>
    /// Lit le CSV et retourne les lignes de données.
    /// </summary>
    /// <param name="reader">Lecteur texte (déjà ouvert sur le fichier en CP1252).</param>
    /// <param name="ct">Jeton d'annulation.</param>
    /// <returns>Les lignes valides, dans l'ordre du fichier.</returns>
    public async Task<IReadOnlyList<CascadeRow>> ParseAsync(TextReader reader, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var rows = new List<CascadeRow>();
        string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);

        // Sauter l'en-tête (potentiellement multi-ligne) : on cherche la
        // première ligne qui commence par un chiffre (= premier Num_Ord).
        while (line is not null && !LooksLikeDataLine(line))
        {
            line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
        }

        while (line is not null)
        {
            ct.ThrowIfCancellationRequested();
            var parsed = TryParseLine(line, rows.Count + 1);
            if (parsed is not null)
            {
                rows.Add(parsed);
            }
            line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
        }

        return rows;
    }

    private static bool LooksLikeDataLine(string line)
    {
        // Heuristique : la ligne de données commence par un entier.
        // On évite ainsi d'avaler les fragments d'en-tête qui contiennent
        // "Indice Minimum", "01/03/2022", etc.
        var trimmed = line.AsSpan().TrimStart();
        if (trimmed.IsEmpty) return false;
        var first = trimmed[0];
        return first is >= '0' and <= '9';
    }

    private static CascadeRow? TryParseLine(string line, int lineNumber)
    {
        var parts = line.Split(';');
        if (parts.Length != ExpectedColumnCount)
        {
            // On ne lève pas : un CSV peut contenir des lignes vides ou
            // parasites. Le nombre de lignes sautées est rapporté par le
            // NomenclatureSeeder via le SeedResult.
            return null;
        }

        try
        {
            return new CascadeRow(
                NumOrd:          int.Parse(parts[0], CultureInfo.InvariantCulture),
                TypeContrat:     NormalizeField(parts[1]),
                TypeFiliere:     NormalizeField(parts[2]),
                TypeSecteur:     NormalizeField(parts[3]),
                TypePersonnel:   NormalizeField(parts[4]),
                CorpsFiliere:    NormalizeField(parts[5]),
                Grade:           NormalizeField(parts[6]),
                Categorie:       int.Parse(parts[7], CultureInfo.InvariantCulture),
                IndiceAv2022_03: int.Parse(parts[8], CultureInfo.InvariantCulture),
                IndiceAp2022_03: int.Parse(parts[9], CultureInfo.InvariantCulture),
                IndiceAp2023_01: int.Parse(parts[10], CultureInfo.InvariantCulture),
                IndiceAp2024_01: int.Parse(parts[11], CultureInfo.InvariantCulture));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Nettoie un champ : trim, supprime les espaces doubles, décode les
    /// guillemets résiduels si le CSV en utilise (le notre n'en a pas dans
    /// les données, mais c'est défensif).
    /// </summary>
    private static string NormalizeField(string raw)
    {
        var s = raw.Trim();
        // Pas de guillemets attendus ici, mais on les retire s'il y en a.
        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
        {
            s = s[1..^1];
        }
        // Espaces multiples → simple.
        while (s.Contains("  ", StringComparison.Ordinal))
        {
            s = s.Replace("  ", " ", StringComparison.Ordinal);
        }
        return s;
    }

    /// <summary>
    /// Ouvre un fichier CSV en CP1252. Encapsule l'enregistrement du
    /// <c>CodePagesEncodingProvider</c> (nécessaire sur .NET 5+).
    /// </summary>
    public static TextReader OpenCp1252(string path)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        // CP1252 = Windows-1252. CodePagesEncodingProvider.Instance.Id=1252.
        var enc = Encoding.GetEncoding(1252);
        return new StreamReader(path, enc, detectEncodingFromByteOrderMarks: true);
    }
}
