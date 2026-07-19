using System.Reflection;
using System.Text.RegularExpressions;
using NetArchTest.Rules;

namespace PaieEducation.Tests.Architecture;

/// <summary>
/// Garde-fou permanent : vérifie la matrice de dépendances de la Clean Architecture
/// (voir docs/CONVENTIONS.md et ADR-0001). Échoue à la moindre dépendance interdite.
/// </summary>
public class DependencyRulesTests
{
    private const string Domain = "PaieEducation.Domain";
    private const string Application = "PaieEducation.Application";
    private const string Infrastructure = "PaieEducation.Infrastructure";
    private const string Persistence = "PaieEducation.Persistence";
    private const string Reporting = "PaieEducation.Reporting";
    private const string Presentation = "PaieEducation.Presentation";
    private const string Shared = "PaieEducation.Shared";

    private static Assembly Load(string name) => Assembly.Load(name);

    [Fact]
    public void Domain_ne_depend_que_de_Shared_hors_technos()
    {
        TestResult result = Types.InAssembly(Load(Domain))
            .ShouldNot()
            .HaveDependencyOnAny(
                Application, Infrastructure, Persistence, Reporting, Presentation,
                "Microsoft.Data.Sqlite", "Dapper", "QuestPDF", "ClosedXML",
                "PresentationFramework", "CommunityToolkit.Mvvm")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Shared_ne_depend_d_aucun_projet_metier()
    {
        TestResult result = Types.InAssembly(Load(Shared))
            .ShouldNot()
            .HaveDependencyOnAny(Domain, Application, Infrastructure, Persistence, Reporting, Presentation)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Application_ne_depend_ni_de_infrastructure_ni_de_persistence()
    {
        TestResult result = Types.InAssembly(Load(Application))
            .ShouldNot()
            .HaveDependencyOnAny(Infrastructure, Persistence, Presentation, Reporting)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    /// <summary>
    /// ADR-0011 — Maintien de <c>decimal</c> + arrondi centralisé. Tout arrondi métier
    /// (bulletin, cotisation, IRG, rappel) doit passer par
    /// <c>PaieEducation.Domain/Calcul/Services/ArrondiService.cs</c>. Aucun appel direct
    /// à <c>Math.Round</c>, <c>decimal.Round</c>, <c>Math.Floor</c>, <c>Math.Ceiling</c>
    /// ou <c>Math.Truncate</c> ne doit apparaître ailleurs dans le code métier
    /// (<c>Domain</c> + <c>Application</c>).
    ///
    /// Implémentation : scan des fichiers source des deux projets, en s'appuyant sur
    /// la position du projet de test dans la solution. Approche délibérément simple
    /// (regex sur .cs) plutôt que scan IL : plus lisible, plus rapide, sans dépendance
    /// externe (Mono.Cecil, etc.). Le test échoue avec un rapport ligne par ligne.
    /// </summary>
    [Fact]
    public void Arrondi_centralise_uniquement_dans_ArrondiService()
    {
        var forbidden = new (string Pattern, string Name)[]
        {
            (@"\bMath\.Round\b",        "Math.Round"),
            (@"\bdecimal\.Round\b",     "decimal.Round"),
            (@"\bMath\.Floor\b",        "Math.Floor"),
            (@"\bMath\.Ceiling\b",      "Math.Ceiling"),
            (@"\bMath\.Truncate\b",     "Math.Truncate"),
        };

        // Chemins racine : on remonte depuis le répertoire de l'assembly de test.
        // Layout attendu : <repo>/tests/PaieEducation.Tests.Architecture/bin/<cfg>/<tfm>/
        //                   <repo>/src/PaieEducation.Domain/
        //                   <repo>/src/PaieEducation.Application/
        var testAsmDir = Path.GetDirectoryName(typeof(DependencyRulesTests).Assembly.Location)!;
        var repoRoot = Path.GetFullPath(Path.Combine(testAsmDir, "..", "..", "..", "..", ".."));

        var roots = new (string Path, string Label)[]
        {
            (Path.Combine(repoRoot, "src", "PaieEducation.Domain"),      "Domain"),
            (Path.Combine(repoRoot, "src", "PaieEducation.Application"), "Application"),
        };

        var violations = new List<string>();
        var allowedFile = "ArrondiService"; // nom du fichier autorisé

        foreach (var (root, label) in roots)
        {
            if (!Directory.Exists(root)) continue;

            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var isArrondiService = fileName == allowedFile;

                // Lecture tolérante (encodage UTF-8 sans BOM attendu, fallback Latin-1).
                string content;
                try { content = File.ReadAllText(file); }
                catch { continue; }

                // On retire les commentaires mono-ligne et multi-lignes pour éviter
                // les faux positifs (un commentaire "voir Math.Round" ne viole pas la règle).
                var stripped = System.Text.RegularExpressions.Regex.Replace(
                    content, @"//[^\n]*", "");
                stripped = System.Text.RegularExpressions.Regex.Replace(
                    stripped, @"/\*.*?\*/", "", RegexOptions.Singleline);

                foreach (var (pattern, name) in forbidden)
                {
                    if (!isArrondiService)
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(stripped, pattern);
                        if (match.Success)
                        {
                            // Récupère le n° de ligne pour un diagnostic exploitable.
                            var line = stripped[..match.Index].Count(c => c == '\n') + 1;
                            violations.Add(
                                $"{label}/{Path.GetRelativePath(root, file)}:{line} → {name}");
                        }
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "ADR-0011 : arrondi centralisé uniquement dans ArrondiService.cs. " +
            "Appels interdits détectés (" + violations.Count + ") :\n  - " +
            string.Join("\n  - ", violations));
    }

    private static string Describe(TestResult result)
        => result.IsSuccessful
            ? "OK"
            : "Types en violation : " + string.Join(", ", result.FailingTypeNames ?? []);
}
