using Microsoft.Data.Sqlite;
using PaieEducation.Infrastructure.Repositories.Payroll;

namespace PaieEducation.Tests.Integration.Repositories;

/// <summary>
/// Tests de <see cref="GrilleIndiciaireRepository"/> (Phase 5, tâche 4,
/// GérerRéférentiels) : écriture versionnée de <c>ValeurPoint</c>,
/// <c>GrilleIndiciaire</c>, <c>IndicesEchelon</c> — symétrique en écriture
/// de <see cref="VariableRepositoryTests"/>.
/// </summary>
public class GrilleIndiciaireRepositoryTests
{
    private static void SeedCategorieEtEchelon(SqliteConnection c) => SchemaTestSupport.Exec(c, """
        INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('13', 13, 'Catégorie 13', '2026-01-01T00:00:00Z', 'h');
        INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('5', 5, 'Échelon 5', '2026-01-01T00:00:00Z', 'h');
        """);

    // ---- ValeurPoint ----

    [Fact]
    public async Task DefinirValeurPointAsync_premiere_definition_n_a_rien_a_fermer()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new GrilleIndiciaireRepository(scope.Conn);

        var result = await repo.DefinirValeurPointAsync(45m, "2007-01-01", "2007", null, DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("VP-2007-01-01", result.Value);
        Assert.Null(SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM ValeurPoint WHERE Id = @id;", ("@id", result.Value)));
    }

    [Fact]
    public async Task DefinirValeurPointAsync_nouvelle_version_ferme_la_precedente_a_la_veille()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new GrilleIndiciaireRepository(scope.Conn);
        var premiere = await repo.DefinirValeurPointAsync(45m, "2007-01-01", "2007", null, DateTimeOffset.UtcNow);

        var seconde = await repo.DefinirValeurPointAsync(50m, "2026-01-01", "2026", "Décret X", DateTimeOffset.UtcNow);

        Assert.True(seconde.IsSuccess, seconde.IsFailure ? seconde.Error.Message : null);
        Assert.Equal("2025-12-31", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM ValeurPoint WHERE Id = @id;", ("@id", premiere.Value)));
        Assert.Null(SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM ValeurPoint WHERE Id = @id;", ("@id", seconde.Value)));
    }

    [Fact]
    public async Task DefinirValeurPointAsync_date_effet_en_double_echoue_en_conflit()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new GrilleIndiciaireRepository(scope.Conn);
        await repo.DefinirValeurPointAsync(45m, "2007-01-01", "2007", null, DateTimeOffset.UtcNow);

        var doublon = await repo.DefinirValeurPointAsync(46m, "2007-01-01", "2007-bis", null, DateTimeOffset.UtcNow);

        Assert.True(doublon.IsFailure);
        Assert.Contains("déjà", doublon.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DefinirValeurPointAsync_date_effet_non_posterieure_a_la_version_en_vigueur_echoue()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new GrilleIndiciaireRepository(scope.Conn);
        await repo.DefinirValeurPointAsync(45m, "2020-01-01", "2020", null, DateTimeOffset.UtcNow);

        var retroactive = await repo.DefinirValeurPointAsync(40m, "2019-01-01", "2019", null, DateTimeOffset.UtcNow);

        Assert.True(retroactive.IsFailure);
        Assert.Contains("postérieure", retroactive.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DefinirValeurPointAsync_valeur_non_positive_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new GrilleIndiciaireRepository(scope.Conn);

        var result = await repo.DefinirValeurPointAsync(0m, "2007-01-01", "2007", null, DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal(0, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM ValeurPoint;"));
    }

    // ---- GrilleIndiciaire ----

    [Fact]
    public async Task DefinirIndiceMinAsync_nouvelle_version_ferme_la_precedente()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedCategorieEtEchelon(scope.Conn);
        var repo = new GrilleIndiciaireRepository(scope.Conn);
        var premiere = await repo.DefinirIndiceMinAsync("13", 500, "2020-01-01", "v1", null, DateTimeOffset.UtcNow);

        var seconde = await repo.DefinirIndiceMinAsync("13", 578, "2024-01-01", "v2", null, DateTimeOffset.UtcNow);

        Assert.True(seconde.IsSuccess, seconde.IsFailure ? seconde.Error.Message : null);
        Assert.Equal("GI-13-2024-01-01", seconde.Value);
        Assert.Equal("2023-12-31", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM GrilleIndiciaire WHERE Id = @id;", ("@id", premiere.Value)));
    }

    [Fact]
    public async Task DefinirIndiceMinAsync_indice_non_positif_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedCategorieEtEchelon(scope.Conn);
        var repo = new GrilleIndiciaireRepository(scope.Conn);

        var result = await repo.DefinirIndiceMinAsync("13", -1, "2020-01-01", "v1", null, DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
    }

    // ---- IndicesEchelon ----

    [Fact]
    public async Task DefinirIndiceEchelonAsync_nouvelle_version_ferme_la_precedente()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedCategorieEtEchelon(scope.Conn);
        var repo = new GrilleIndiciaireRepository(scope.Conn);
        var premiere = await repo.DefinirIndiceEchelonAsync("5", 80, "2020-01-01", "v1", null, DateTimeOffset.UtcNow);

        var seconde = await repo.DefinirIndiceEchelonAsync("5", 100, "2024-01-01", "v2", null, DateTimeOffset.UtcNow);

        Assert.True(seconde.IsSuccess, seconde.IsFailure ? seconde.Error.Message : null);
        Assert.Equal("IE-5-2024-01-01", seconde.Value);
        Assert.Equal("2023-12-31", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM IndicesEchelon WHERE Id = @id;", ("@id", premiere.Value)));
    }

    [Fact]
    public async Task DefinirIndiceEchelonAsync_indice_negatif_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedCategorieEtEchelon(scope.Conn);
        var repo = new GrilleIndiciaireRepository(scope.Conn);

        var result = await repo.DefinirIndiceEchelonAsync("5", -1, "2020-01-01", "v1", null, DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
    }
}
