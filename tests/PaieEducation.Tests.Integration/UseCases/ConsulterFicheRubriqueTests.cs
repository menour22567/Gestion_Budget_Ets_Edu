using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Infrastructure.Repositories.Workbench;
using PaieEducation.Seeding;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Preuve d'intégration de <see cref="ConsulterFicheRubrique"/> (Phase 6,
/// tâche 4) — rejoue les données ISSRP réelles (<see cref="ReglementaireSeeder"/>).
/// </summary>
public class ConsulterFicheRubriqueTests
{
    [Fact]
    public async Task Executer_agrege_identite_bareme_et_eligibilite_pour_QUALIF()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await new ReglementaireSeeder().SeedAsync(scope.Conn);
        var useCase = new ConsulterFicheRubrique(new WorkbenchReadRepository(scope.Conn));

        var result = await useCase.ExecuterAsync(new ConsulterFicheRubrique.Demande("QUALIF", "2025-06-01"));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("QUALIF", result.Value.Detail.Id);
        Assert.NotEmpty(result.Value.Baremes);
    }

    [Fact]
    public async Task Executer_rubrique_inexistante_echoue_avec_not_found()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await new ReglementaireSeeder().SeedAsync(scope.Conn);
        var useCase = new ConsulterFicheRubrique(new WorkbenchReadRepository(scope.Conn));

        var result = await useCase.ExecuterAsync(new ConsulterFicheRubrique.Demande("N_EXISTE_PAS", "2025-06-01"));

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }
}
