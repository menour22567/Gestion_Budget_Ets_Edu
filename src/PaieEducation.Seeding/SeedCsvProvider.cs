using System.Reflection;
using Microsoft.Data.Sqlite;
using PaieEducation.Seeding.Models;

namespace PaieEducation.Seeding;

/// <summary>
/// Lecteur du jeu de données nomenclature embarqué en ressource
/// (<c>PaieEducation.Seeding.Data.Cascade_Corps_Grades_30526.csv</c>).
/// Remplace le chemin <c>--csv</c> externe de la CLI : la base peut être
/// initialisée hors-ligne sans aucun fichier adjacent (C1.1).
/// </summary>
public static class SeedCsvProvider
{
    private const string ResourceName =
        "PaieEducation.Seeding.Data.Cascade_Corps_Grades_30526.csv";

    /// <summary>
    /// Lit et parse le CSV cascade embarqué. Le parseur CP1252 (accents)
    /// est réutilisé tel quel.
    /// </summary>
    public static async Task<IReadOnlyList<CascadeRow>> ReadEmbeddedRowsAsync(
        CancellationToken ct = default)
    {
        var assembly = typeof(SeedCsvProvider).Assembly;
        await using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Ressource embarquée introuvable : {ResourceName}");

        var parser = new CsvCascadeParser();
        IReadOnlyList<CascadeRow> rows;
        using (var reader = CsvCascadeParser.OpenCp1252Stream(stream))
        {
            rows = await parser.ParseAsync(reader, ct).ConfigureAwait(false);
        }
        return rows;
    }
}
