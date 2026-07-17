using Microsoft.Data.Sqlite;
using PaieEducation.Infrastructure.Repositories.Workbench;
using PaieEducation.Tools.Seeding;

namespace PaieEducation.Tests.Integration.Repositories;

/// <summary>
/// Tests des 4 méthodes de <see cref="WorkbenchReadRepository"/> ajoutées
/// pour la matrice de couverture (Phase 5, tâche 5, D11) — données réelles
/// via <see cref="ReglementaireSeeder"/> (grades hors catégorie IDLS-G144
/// et suivants, seuls grades réellement insérés par ce seeder, cf. J4E/Q-C3).
/// </summary>
public class WorkbenchReadRepositoryTests
{
    private static async Task<SqliteConnection> SeederAsync(SqliteConnection conn)
    {
        await new ReglementaireSeeder().SeedAsync(conn);
        return conn;
    }

    [Fact]
    public async Task ListerCorpsActifsAsync_renvoie_les_corps_seedes()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeederAsync(scope.Conn);
        var repo = new WorkbenchReadRepository(scope.Conn);

        var corps = await repo.ListerCorpsActifsAsync();

        Assert.Contains(corps, c => c.Id == "IDLS");
    }

    [Fact]
    public async Task ListerRubriquesActivesAsync_renvoie_ISSRP_45()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeederAsync(scope.Conn);
        var repo = new WorkbenchReadRepository(scope.Conn);

        var rubriques = await repo.ListerRubriquesActivesAsync();

        Assert.Contains(rubriques, r => r.Id == "ISSRP_45");
    }

    [Fact]
    public async Task ListerGradesActifsAsync_resout_IDLS_G144_vers_le_corps_IDLS()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeederAsync(scope.Conn);
        var repo = new WorkbenchReadRepository(scope.Conn);

        var grades = await repo.ListerGradesActifsAsync();

        Assert.Contains(grades, g => g.GradeId == "IDLS-G144" && g.CorpsId == "IDLS");
    }

    [Fact]
    public async Task ListerConditionsCorpsGradeAsync_ne_renvoie_que_GRADE_et_CORPS_actives_et_expirees()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeederAsync(scope.Conn);
        var repo = new WorkbenchReadRepository(scope.Conn);

        var conditions = await repo.ListerConditionsCorpsGradeAsync();

        Assert.NotEmpty(conditions);
        Assert.All(conditions, c => Assert.True(c.CritereId is "GRADE" or "CORPS"));
        // GE-ISSRP15-HIST (expirée, DateFin 2024-12-31) doit être incluse — pas de filtre par date.
        Assert.Contains(conditions, c => c.RubriqueId == "ISSRP_15" && c.Periode.DateFin == "2024-12-31");
    }
}
