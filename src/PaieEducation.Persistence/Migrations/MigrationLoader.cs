using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PaieEducation.Persistence.Migrations;

/// <summary>
/// Charge les scripts de migration depuis les ressources embarquées d'un assembly.
/// </summary>
/// <remarks>
/// <para>Convention de nommage des fichiers : <c>V&lt;numéro&gt;__&lt;nom&gt;.sql</c>.</para>
/// <para>Exemples : <c>V001__init.sql</c>, <c>V002__structure_carriere.sql</c>.</para>
/// <para>Le numéro doit être un entier non signé croissant. Le nom est libre mais doit
/// rester lisible (utilisé dans les logs et la table <c>SchemaVersions</c>).</para>
/// <para>Les fichiers sont rangés sous <c>Migrations/</c> dans le projet, embarqués via
/// <c>&lt;EmbeddedResource Include="Migrations\*.sql" /&gt;</c> dans le .csproj.</para>
/// </remarks>
public static class MigrationLoader
{
    private static readonly Regex VersionPattern = new(
        @"^V(?<v>\d+)__(?<n>.+)\.sql$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Charge et trie les migrations par version croissante. Lève une
    /// <see cref="InvalidOperationException"/> si un nom de fichier ne respecte
    /// pas la convention, ou si deux migrations portent le même numéro.
    /// </summary>
    /// <param name="assembly">Assembly contenant les ressources <c>.sql</c>.</param>
    /// <param name="resourcePrefix">
    /// Préfixe commun des ressources (ex. <c>PaieEducation.Persistence.Migrations.</c>).
    /// Seules les ressources dont le nom commence par ce préfixe sont considérées.
    /// </param>
    public static IReadOnlyList<Migration> LoadFromAssembly(Assembly assembly, string resourcePrefix)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourcePrefix);

        var names = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(resourcePrefix, StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var migrations = new List<Migration>(names.Count);
        var seenVersions = new HashSet<int>();

        foreach (var resourceName in names)
        {
            var fileName = resourceName[resourcePrefix.Length..];
            var match = VersionPattern.Match(fileName);
            if (!match.Success)
            {
                throw new InvalidOperationException(
                    $"La ressource '{resourceName}' ne respecte pas la convention V<numéro>__<nom>.sql.");
            }

            var version = int.Parse(match.Groups["v"].Value, CultureInfo.InvariantCulture);
            if (!seenVersions.Add(version))
            {
                throw new InvalidOperationException(
                    $"Numéro de migration en doublon : V{version}.");
            }

            var name = match.Groups["n"].Value;
            var sql = ReadResource(assembly, resourceName);

            migrations.Add(new Migration(version, name, sql));
        }

        return migrations;
    }

    private static string ReadResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Ressource introuvable : {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
