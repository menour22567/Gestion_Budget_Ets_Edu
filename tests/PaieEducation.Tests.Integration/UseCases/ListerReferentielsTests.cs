using Microsoft.Data.Sqlite;
using PaieEducation.Application.Referentiels.UseCases;
using PaieEducation.Infrastructure.Repositories.Payroll;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Preuve d'intégration de <see cref="ListerReferentiels"/> (Phase 6,
/// sélecteurs référentiels) : agrège Grades/Catégories/Échelons en un
/// seul aller-retour.
/// </summary>
public class ListerReferentielsTests
{
    private static void SeedNomenclature(SqliteConnection c) => SchemaTestSupport.Exec(c, """
        INSERT INTO Filieres (Id, Libelle, CreatedAt, Hash) VALUES ('ENSEIGNANT', 'Enseignant', '2026-01-01T00:00:00Z', 'h');
        INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ('PDLP', 'Prof. École primaire', 'ENSEIGNANT', '2026-01-01T00:00:00Z', 'h');
        INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, Actif, CreatedAt, Hash) VALUES ('PDLP-G105', 'Professeur de l''Ecole primaire', 'PDLP', 1, 1, '2026-01-01T00:00:00Z', 'h');
        INSERT INTO Categories (Id, Niveau, Libelle, Actif, CreatedAt, Hash) VALUES ('13', 13, 'Catégorie 13', 1, '2026-01-01T00:00:00Z', 'h');
        INSERT INTO Echelons (Id, Numero, Libelle, Actif, CreatedAt, Hash) VALUES ('5', 5, 'Échelon 5', 1, '2026-01-01T00:00:00Z', 'h');
        """);

    [Fact]
    public async Task Executer_agrege_les_3_listes_en_un_seul_appel()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedNomenclature(scope.Conn);
        var useCase = new ListerReferentiels(new ReferentielReadRepository(scope.Conn));

        var result = await useCase.ExecuterAsync();

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Contains(result.Value.Grades, g => g.Id == "PDLP-G105");
        Assert.Contains(result.Value.Categories, c => c.Id == "13");
        Assert.Contains(result.Value.Echelons, e => e.Id == "5");
    }
}
