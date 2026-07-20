using Microsoft.Data.Sqlite;
using PaieEducation.Infrastructure.Repositories.Workbench;

namespace PaieEducation.Tests.Integration.Repositories;

/// <summary>
/// Tests de <see cref="RubriqueBaremeRepository"/> (chantier P5, audit du
/// 19/07/2026 — 1er chemin d'écriture pour <c>RubriqueBaremes</c>, jusqu'ici
/// seedée uniquement). Même patron « ferme puis insère » que
/// <see cref="GrilleIndiciaireRepositoryTests"/> (ADR-0008).
/// </summary>
public class RubriqueBaremeRepositoryTests
{
    private static void SeedRubrique(SqliteConnection c, string id = "QUALIF") => SchemaTestSupport.Exec(c, $"""
        INSERT INTO Rubriques (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul, CreatedAt, Hash)
        VALUES ('{id}', 'Qualification', 'GAIN', 'TBASE', 'MENSUELLE', 10, '2026-01-01T00:00:00Z', 'h');
        """);

    [Fact]
    public async Task DefinirValeurBaremeAsync_premiere_definition_n_a_rien_a_fermer()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new RubriqueBaremeRepository(scope.Conn);

        var result = await repo.DefinirValeurBaremeAsync(
            "QUALIF", "CATEGORIE", "13", null, "TAUX", "0.45", "2020-01-01", "Décret X", DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("RB-QUALIF-CATEGORIE-13-2020-01-01", result.Value);
        Assert.Null(SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM RubriqueBaremes WHERE Id = @id;", ("@id", result.Value)));
        Assert.Equal("0.45", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT Valeur FROM RubriqueBaremes WHERE Id = @id;", ("@id", result.Value)));
    }

    [Fact]
    public async Task DefinirValeurBaremeAsync_nouvelle_version_de_la_meme_tranche_ferme_la_precedente_a_la_veille()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new RubriqueBaremeRepository(scope.Conn);
        var premiere = await repo.DefinirValeurBaremeAsync(
            "QUALIF", "CATEGORIE", "13", null, "TAUX", "0.40", "2020-01-01", null, DateTimeOffset.UtcNow);

        var seconde = await repo.DefinirValeurBaremeAsync(
            "QUALIF", "CATEGORIE", "13", null, "TAUX", "0.45", "2024-01-01", "Décret Y", DateTimeOffset.UtcNow);

        Assert.True(seconde.IsSuccess, seconde.IsFailure ? seconde.Error.Message : null);
        Assert.Equal("2023-12-31", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM RubriqueBaremes WHERE Id = @id;", ("@id", premiere.Value)));
        Assert.Null(SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM RubriqueBaremes WHERE Id = @id;", ("@id", seconde.Value)));
    }

    [Fact]
    public async Task DefinirValeurBaremeAsync_tranches_distinctes_de_la_meme_rubrique_n_interferent_pas()
    {
        // Deux BorneInf différents ("13" et "1") sont des tranches indépendantes :
        // définir l'une ne doit pas fermer ou bloquer l'autre.
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new RubriqueBaremeRepository(scope.Conn);
        await repo.DefinirValeurBaremeAsync(
            "QUALIF", "CATEGORIE", "1", "12", "TAUX", "0.40", "2020-01-01", null, DateTimeOffset.UtcNow);

        var autreTranche = await repo.DefinirValeurBaremeAsync(
            "QUALIF", "CATEGORIE", "13", null, "TAUX", "0.45", "2020-01-01", null, DateTimeOffset.UtcNow);

        Assert.True(autreTranche.IsSuccess, autreTranche.IsFailure ? autreTranche.Error.Message : null);
        Assert.Null(SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM RubriqueBaremes WHERE BorneInf = '1';"));
        Assert.Null(SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM RubriqueBaremes WHERE BorneInf = '13';"));
    }

    [Fact]
    public async Task DefinirValeurBaremeAsync_date_effet_en_double_pour_la_meme_tranche_echoue_en_conflit()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new RubriqueBaremeRepository(scope.Conn);
        await repo.DefinirValeurBaremeAsync(
            "QUALIF", "CATEGORIE", "13", null, "TAUX", "0.40", "2020-01-01", null, DateTimeOffset.UtcNow);

        var doublon = await repo.DefinirValeurBaremeAsync(
            "QUALIF", "CATEGORIE", "13", null, "TAUX", "0.41", "2020-01-01", null, DateTimeOffset.UtcNow);

        Assert.True(doublon.IsFailure);
        Assert.Equal("conflict", doublon.Error.Code);
    }

    [Fact]
    public async Task DefinirValeurBaremeAsync_date_effet_non_posterieure_a_la_version_en_vigueur_echoue()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new RubriqueBaremeRepository(scope.Conn);
        await repo.DefinirValeurBaremeAsync(
            "QUALIF", "CATEGORIE", "13", null, "TAUX", "0.40", "2020-01-01", null, DateTimeOffset.UtcNow);

        var retroactive = await repo.DefinirValeurBaremeAsync(
            "QUALIF", "CATEGORIE", "13", null, "TAUX", "0.35", "2019-01-01", null, DateTimeOffset.UtcNow);

        Assert.True(retroactive.IsFailure);
        Assert.Equal("validation", retroactive.Error.Code);
    }

    [Fact]
    public async Task DefinirValeurBaremeAsync_rubrique_inconnue_echoue_avec_not_found()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new RubriqueBaremeRepository(scope.Conn);

        var result = await repo.DefinirValeurBaremeAsync(
            "INEXISTANTE", "CATEGORIE", "13", null, "TAUX", "0.45", "2020-01-01", null, DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal(0, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM RubriqueBaremes;"));
    }

    [Fact]
    public async Task DefinirValeurBaremeAsync_dimension_invalide_echoue_explicitement_sans_toucher_la_base()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new RubriqueBaremeRepository(scope.Conn);

        // GRADE est valide pour l'enum BaremeDimension (ReglesEligibilite) mais
        // rejeté par le CHECK de RubriqueBaremes — doit échouer proprement, pas
        // lever une exception SQLite.
        var result = await repo.DefinirValeurBaremeAsync(
            "QUALIF", "GRADE", "G001", null, "TAUX", "0.45", "2020-01-01", null, DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal(0, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM RubriqueBaremes;"));
    }

    [Fact]
    public async Task DefinirValeurBaremeAsync_type_valeur_invalide_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new RubriqueBaremeRepository(scope.Conn);

        var result = await repo.DefinirValeurBaremeAsync(
            "QUALIF", "CATEGORIE", "13", null, "POURCENTAGE", "0.45", "2020-01-01", null, DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
    }

    [Fact]
    public async Task DefinirValeurBaremeAsync_borneSup_null_est_persiste_comme_infini()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new RubriqueBaremeRepository(scope.Conn);

        var result = await repo.DefinirValeurBaremeAsync(
            "QUALIF", "CATEGORIE", "13", null, "TAUX", "0.45", "2020-01-01", null, DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Null(SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT BorneSup FROM RubriqueBaremes WHERE Id = @id;", ("@id", result.Value)));
    }
}
