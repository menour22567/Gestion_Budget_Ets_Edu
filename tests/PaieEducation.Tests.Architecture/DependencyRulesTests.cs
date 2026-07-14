using System.Reflection;
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
    public void Domain_ne_depend_d_aucun_projet_ni_techno()
    {
        TestResult result = Types.InAssembly(Load(Domain))
            .ShouldNot()
            .HaveDependencyOnAny(
                Application, Infrastructure, Persistence, Reporting, Presentation, Shared,
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

    private static string Describe(TestResult result)
        => result.IsSuccessful
            ? "OK"
            : "Types en violation : " + string.Join(", ", result.FailingTypeNames ?? []);
}
