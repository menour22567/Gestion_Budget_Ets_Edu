using Microsoft.Data.Sqlite;
using PaieEducation.Infrastructure.Repositories.Payroll;

namespace PaieEducation.Tests.Integration.Repositories;

/// <summary>
/// Tests de <see cref="ReferentielReadRepository"/> (Phase 6, sélecteurs
/// référentiels) : lecture de <c>Grades</c>/<c>Categories</c>/<c>Echelons</c>
/// (V002), filtrée sur <c>Actif = 1</c>.
/// </summary>
public class ReferentielReadRepositoryTests
{
    private static void SeedNomenclature(SqliteConnection c) => SchemaTestSupport.Exec(c, """
        INSERT INTO Filieres (Id, Libelle, CreatedAt, Hash) VALUES ('ENSEIGNANT', 'Enseignant', '2026-01-01T00:00:00Z', 'h');
        INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ('PDLP', 'Prof. École primaire', 'ENSEIGNANT', '2026-01-01T00:00:00Z', 'h');
        INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, Actif, CreatedAt, Hash) VALUES ('PDLP-G105', 'Professeur de l''Ecole primaire', 'PDLP', 1, 1, '2026-01-01T00:00:00Z', 'h');
        INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, Actif, CreatedAt, Hash) VALUES ('PDLP-G106', 'Ancien grade', 'PDLP', 2, 0, '2026-01-01T00:00:00Z', 'h');
        INSERT INTO Categories (Id, Niveau, Libelle, Actif, CreatedAt, Hash) VALUES ('13', 13, 'Catégorie 13', 1, '2026-01-01T00:00:00Z', 'h');
        INSERT INTO Echelons (Id, Numero, Libelle, Actif, CreatedAt, Hash) VALUES ('5', 5, 'Échelon 5', 1, '2026-01-01T00:00:00Z', 'h');
        """);

    [Fact]
    public async Task ListerGradesAsync_ne_renvoie_que_les_grades_actifs()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedNomenclature(scope.Conn);
        var repo = new ReferentielReadRepository(scope.Conn);

        var result = await repo.ListerGradesAsync();

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Single(result.Value);
        Assert.Equal("PDLP-G105", result.Value[0].Id);
        Assert.Equal("Professeur de l'Ecole primaire", result.Value[0].Libelle);
    }

    [Fact]
    public async Task ListerCategoriesAsync_renvoie_les_categories_actives()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedNomenclature(scope.Conn);
        var repo = new ReferentielReadRepository(scope.Conn);

        var result = await repo.ListerCategoriesAsync();

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Contains(result.Value, c => c.Id == "13" && c.Libelle == "Catégorie 13");
    }

    [Fact]
    public async Task ListerEchelonsAsync_renvoie_les_echelons_actifs()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedNomenclature(scope.Conn);
        var repo = new ReferentielReadRepository(scope.Conn);

        var result = await repo.ListerEchelonsAsync();

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Contains(result.Value, e => e.Id == "5" && e.Libelle == "Échelon 5");
    }
}
