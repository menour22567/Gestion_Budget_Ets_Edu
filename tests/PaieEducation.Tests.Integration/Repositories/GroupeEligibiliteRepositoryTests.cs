using Microsoft.Data.Sqlite;
using PaieEducation.Infrastructure.Repositories.Workbench;

namespace PaieEducation.Tests.Integration.Repositories;

/// <summary>
/// Tests de <see cref="GroupeEligibiliteRepository"/> (chantier P6, audit du
/// 19/07/2026 — 1er chemin d'écriture pour <c>GroupesEligibilite</c>,
/// jusqu'ici seedée uniquement). Contrairement à <c>RubriqueBaremeRepository</c>,
/// l'Id est un code métier fourni par l'appelant : pas de « ferme puis
/// insère » — création pure + clôture pure.
/// </summary>
public class GroupeEligibiliteRepositoryTests
{
    private static void SeedRubrique(SqliteConnection c, string id = "TEST_DNF") => SchemaTestSupport.Exec(c, $"""
        INSERT INTO Rubriques (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul, CreatedAt, Hash)
        VALUES ('{id}', 'Test DNF', 'GAIN', 'TBASE', 'MENSUELLE', 10, '2026-01-01T00:00:00Z', 'h');
        """);

    [Fact]
    public async Task DefinirGroupeAsync_creation_nominale_reussit()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new GroupeEligibiliteRepository(scope.Conn);

        var result = await repo.DefinirGroupeAsync(
            "GE-TEST", "TEST_DNF", "INFO", null, 100, "2026-01-01", null, "Décret X", "workbench", DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("GE-TEST", result.Value);
        Assert.Null(SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM GroupesEligibilite WHERE Id = 'GE-TEST';"));
        Assert.Equal("workbench", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT CreatedBy FROM GroupesEligibilite WHERE Id = 'GE-TEST';"));
    }

    [Fact]
    public async Task DefinirGroupeAsync_id_deja_utilise_echoue_en_conflit()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new GroupeEligibiliteRepository(scope.Conn);
        await repo.DefinirGroupeAsync(
            "GE-TEST", "TEST_DNF", "INFO", null, 100, "2026-01-01", null, null, "workbench", DateTimeOffset.UtcNow);

        var doublon = await repo.DefinirGroupeAsync(
            "GE-TEST", "TEST_DNF", "INFO", null, 100, "2026-06-01", null, null, "workbench", DateTimeOffset.UtcNow);

        Assert.True(doublon.IsFailure);
        Assert.Equal("conflict", doublon.Error.Code);
    }

    [Fact]
    public async Task DefinirGroupeAsync_rubrique_inconnue_echoue_avec_not_found()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new GroupeEligibiliteRepository(scope.Conn);

        var result = await repo.DefinirGroupeAsync(
            "GE-TEST", "INEXISTANTE", "INFO", null, 100, "2026-01-01", null, null, "workbench", DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal(0, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM GroupesEligibilite;"));
    }

    [Fact]
    public async Task DefinirGroupeAsync_severite_invalide_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new GroupeEligibiliteRepository(scope.Conn);

        var result = await repo.DefinirGroupeAsync(
            "GE-TEST", "TEST_DNF", "URGENTE", null, 100, "2026-01-01", null, null, "workbench", DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal(0, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM GroupesEligibilite;"));
    }

    [Fact]
    public async Task DefinirGroupeAsync_messageId_inconnu_echoue_avec_not_found()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new GroupeEligibiliteRepository(scope.Conn);

        var result = await repo.DefinirGroupeAsync(
            "GE-TEST", "TEST_DNF", "INFO", "MSG-INEXISTANT", 100, "2026-01-01", null, null, "workbench", DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task CloreGroupeAsync_groupe_ouvert_est_ferme_a_la_date_demandee()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new GroupeEligibiliteRepository(scope.Conn);
        await repo.DefinirGroupeAsync(
            "GE-TEST", "TEST_DNF", "INFO", null, 100, "2026-01-01", null, null, "workbench", DateTimeOffset.UtcNow);

        var result = await repo.CloreGroupeAsync("GE-TEST", "2026-12-31");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("2026-12-31", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM GroupesEligibilite WHERE Id = 'GE-TEST';"));
    }

    [Fact]
    public async Task CloreGroupeAsync_groupe_deja_clos_echoue_en_conflit()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new GroupeEligibiliteRepository(scope.Conn);
        await repo.DefinirGroupeAsync(
            "GE-TEST", "TEST_DNF", "INFO", null, 100, "2026-01-01", null, null, "workbench", DateTimeOffset.UtcNow);
        await repo.CloreGroupeAsync("GE-TEST", "2026-12-31");

        var result = await repo.CloreGroupeAsync("GE-TEST", "2027-01-01");

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
    }

    [Fact]
    public async Task CloreGroupeAsync_groupe_inconnu_echoue_avec_not_found()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new GroupeEligibiliteRepository(scope.Conn);

        var result = await repo.CloreGroupeAsync("GE-INEXISTANT", "2026-12-31");

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }
}
