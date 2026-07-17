using Microsoft.Data.Sqlite;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Infrastructure.Repositories.Workbench;
using PaieEducation.Tools.Seeding;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Preuve d'intégration de <see cref="ListerMatriceCouverture"/> (Phase 5,
/// tâche 5, D11) — rejoue les données ISSRP réelles (<see cref="ReglementaireSeeder"/>),
/// seul exemple détaillé et validé du projet, via le corps <c>IDLS</c> (les
/// 4 grades hors catégorie IDLS-G144/145/146/148 sont les seuls grades
/// ISSRP réellement insérés par ce seeder, cf. J4E/Q-C3).
/// </summary>
public class ListerMatriceCouvertureTests
{
    private static ListerMatriceCouverture BuildUseCase(SqliteConnection conn)
        => new(new WorkbenchReadRepository(conn));

    [Fact]
    public async Task IDLS_est_couvert_et_actif_pour_ISSRP_45_via_IDLS_G144()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await new ReglementaireSeeder().SeedAsync(scope.Conn);

        var useCase = BuildUseCase(scope.Conn);
        var result = await useCase.ExecuterAsync(new ListerMatriceCouverture.Demande("2025-06-01"));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        var cellule = Assert.Single(result.Value, c => c.CorpsId == "IDLS" && c.RubriqueId == "ISSRP_45");
        Assert.True(cellule.Couverte);
        Assert.True(cellule.Active);
    }

    [Fact]
    public async Task IDLS_est_couvert_mais_inactif_pour_ISSRP_15_via_le_groupe_historique_expire()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await new ReglementaireSeeder().SeedAsync(scope.Conn);

        var useCase = BuildUseCase(scope.Conn);
        // IDLS-G144 n'apparaît que dans IssrpHistGrades (GE-ISSRP15-HIST, expiré
        // 2024-12-31) pour ISSRP_15 — pas dans Issrp15DirectGrades (2025+,
        // IDLS-G147, jamais seedé comme ligne Grades par ce seeder).
        var result = await useCase.ExecuterAsync(new ListerMatriceCouverture.Demande("2025-06-01"));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        var cellule = Assert.Single(result.Value, c => c.CorpsId == "IDLS" && c.RubriqueId == "ISSRP_15");
        Assert.True(cellule.Couverte);
        Assert.False(cellule.Active);
    }

    [Fact]
    public async Task IDLS_n_est_pas_couvert_pour_QUALIF_aucune_condition_GRADE_CORPS()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await new ReglementaireSeeder().SeedAsync(scope.Conn);

        var useCase = BuildUseCase(scope.Conn);
        var result = await useCase.ExecuterAsync(new ListerMatriceCouverture.Demande("2025-06-01"));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        var cellule = Assert.Single(result.Value, c => c.CorpsId == "IDLS" && c.RubriqueId == "QUALIF");
        Assert.False(cellule.Couverte);
        Assert.False(cellule.Active);
    }
}
