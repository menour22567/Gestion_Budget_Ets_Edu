using System.Globalization;
using System.Text;
using PaieEducation.Seeding.Models;

namespace PaieEducation.Seeding;

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
        string? record = await ReadLogicalRecordAsync(reader, ct).ConfigureAwait(false);

        // Sauter l'en-tête (potentiellement multi-ligne, y compris à l'intérieur
        // de champs entre guillemets) : on cherche le premier enregistrement
        // logique qui commence par un chiffre (= premier Num_Ord).
        while (record is not null && !LooksLikeDataLine(record))
        {
            record = await ReadLogicalRecordAsync(reader, ct).ConfigureAwait(false);
        }

        while (record is not null)
        {
            ct.ThrowIfCancellationRequested();
            var parsed = TryParseLine(record, rows.Count + 1);
            if (parsed is not null)
            {
                rows.Add(parsed);
            }
            record = await ReadLogicalRecordAsync(reader, ct).ConfigureAwait(false);
        }

        return rows;
    }

    /// <summary>
    /// Lit un enregistrement CSV logique : une ou plusieurs lignes physiques,
    /// fusionnées tant qu'un champ entre guillemets contient un retour chariot
    /// littéral (ex. libellé de grade sur deux lignes dans la source). La
    /// parité du nombre de guillemets accumulés indique si on est « à
    /// l'intérieur » d'un champ ouvert — y compris avec des guillemets doublés
    /// (échappement RFC4180 <c>""</c>), qui contribuent toujours un nombre pair
    /// et ne perturbent donc pas la détection de limite d'enregistrement.
    /// </summary>
    private static async Task<string?> ReadLogicalRecordAsync(TextReader reader, CancellationToken ct)
    {
        string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
        if (line is null) return null;

        var buffer = line;
        while (CountQuotes(buffer) % 2 != 0)
        {
            var next = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (next is null) break; // guillemet non refermé en fin de fichier : au mieux.
            buffer = buffer + "\n" + next;
        }
        return buffer;
    }

    private static int CountQuotes(string s)
    {
        var count = 0;
        foreach (var c in s)
        {
            if (c == '"') count++;
        }
        return count;
    }

    private static bool LooksLikeDataLine(string record)
    {
        // Heuristique : l'enregistrement de données commence par un entier.
        // On évite ainsi d'avaler les fragments d'en-tête qui contiennent
        // "Indice Minimum", "01/03/2022", etc.
        var trimmed = record.AsSpan().TrimStart();
        if (trimmed.IsEmpty) return false;
        var first = trimmed[0];
        return first is >= '0' and <= '9';
    }

    private static CascadeRow? TryParseLine(string record, int lineNumber)
    {
        var parts = SplitCsvRecord(record);
        if (parts.Count != ExpectedColumnCount)
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
    /// Découpe un enregistrement logique en champs, séparateur <c>;</c>, en
    /// respectant les guillemets : un <c>;</c> à l'intérieur d'un champ entre
    /// guillemets n'est pas un délimiteur, et <c>""</c> représente un
    /// guillemet littéral (échappement RFC4180). Nécessaire depuis que
    /// <see cref="ReadLogicalRecordAsync"/> peut fusionner plusieurs lignes
    /// physiques (champ avec retour chariot littéral, ex. libellé de grade
    /// sur deux lignes dans la source).
    /// </summary>
    private static List<string> SplitCsvRecord(string record)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < record.Length; i++)
        {
            var c = record[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < record.Length && record[i + 1] == '"')
                    {
                        field.Append('"');
                        i++; // guillemet doublé = un seul guillemet littéral
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ';')
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(c);
            }
        }
        fields.Add(field.ToString());
        return fields;
    }

    /// <summary>
    /// Nettoie un champ déjà dé-quoté par <see cref="SplitCsvRecord"/> : trim,
    /// remplace les retours chariot littéraux (champ reconstruit sur plusieurs
    /// lignes physiques, cf. <see cref="ReadLogicalRecordAsync"/>) par un
    /// espace, puis supprime les espaces doubles.
    /// </summary>
    private static string NormalizeField(string raw)
    {
        var s = raw.Replace("\r\n", " ", StringComparison.Ordinal)
                    .Replace('\n', ' ')
                    .Replace('\r', ' ')
                    .Trim();
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

    /// <summary>
    /// Ouvre un flux CSV en CP1252 (équivalent de <see cref="OpenCp1252"/>
    /// pour une ressource embarquée).
    /// </summary>
    public static TextReader OpenCp1252Stream(Stream stream)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var enc = Encoding.GetEncoding(1252);
        return new StreamReader(stream, enc, detectEncodingFromByteOrderMarks: true);
    }
}
