using Microsoft.Data.Sqlite;
using PaieEducation.Infrastructure.Repositories.Workbench;

namespace PaieEducation.Tests.Integration.Repositories;

/// <summary>
/// Tests de <see cref="RegleEligibiliteRepository"/> (chantier P6, audit du
/// 19/07/2026 — 1er chemin d'écriture pour <c>ReglesEligibilite</c>). Même
/// patron « ferme puis insère » que <see cref="RubriqueBaremeRepositoryTests"/>
/// (ADR-0008), sur la clé logique <c>(RubriqueId, CritereId, GroupeId)</c>.
/// <c>CriteresEligibilite</c> (dont <c>GRADE</c>) est seedé par la migration
/// V009 — pas besoin de le seeder ici.
/// </summary>
public class RegleEligibiliteRepositoryTests
{
    private static void SeedRubrique(SqliteConnection c, string id = "TEST_DNF") => SchemaTestSupport.Exec(c, $"""
        INSERT INTO Rubriques (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul, CreatedAt, Hash)
        VALUES ('{id}', 'Test DNF', 'GAIN', 'TBASE', 'MENSUELLE', 10, '2026-01-01T00:00:00Z', 'h');
        """);

    private static void SeedGroupe(SqliteConnection c, string id = "GE-TEST", string rubriqueId = "TEST_DNF") => SchemaTestSupport.Exec(c, $"""
        INSERT INTO GroupesEligibilite (Id, RubriqueId, Severite, Priorite, DateEffet, Hash, CreatedAt, CreatedBy)
        VALUES ('{id}', '{rubriqueId}', 'INFO', 100, '2026-01-01', 'h', '2026-01-01T00:00:00Z', 'workbench');
        """);

    [Fact]
    public async Task DefinirRegleAsync_premiere_definition_condition_commune_reussit()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new RegleEligibiliteRepository(scope.Conn);

        var result = await repo.DefinirRegleAsync(
            "TEST_DNF", "GRADE", null, "=", "G-TEST", "2020-01-01", "Décret X", DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Null(SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM ReglesEligibilite WHERE Id = @id;", ("@id", result.Value)));
        Assert.Null(SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT GroupeId FROM ReglesEligibilite WHERE Id = @id;", ("@id", result.Value)));
    }

    [Fact]
    public async Task DefinirRegleAsync_condition_de_groupe_reussit_et_conserve_le_groupeId()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        SeedGroupe(scope.Conn);
        var repo = new RegleEligibiliteRepository(scope.Conn);

        var result = await repo.DefinirRegleAsync(
            "TEST_DNF", "GRADE", "GE-TEST", "IN", "G-TEST,G-AUTRE", "2020-01-01", null, DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("GE-TEST", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT GroupeId FROM ReglesEligibilite WHERE Id = @id;", ("@id", result.Value)));
    }

    [Fact]
    public async Task DefinirRegleAsync_nouvelle_version_de_la_meme_condition_ferme_la_precedente_a_la_veille()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new RegleEligibiliteRepository(scope.Conn);
        var premiere = await repo.DefinirRegleAsync(
            "TEST_DNF", "GRADE", null, "=", "G-TEST", "2020-01-01", null, DateTimeOffset.UtcNow);

        var seconde = await repo.DefinirRegleAsync(
            "TEST_DNF", "GRADE", null, "=", "G-AUTRE", "2024-01-01", "Décret Y", DateTimeOffset.UtcNow);

        Assert.True(seconde.IsSuccess, seconde.IsFailure ? seconde.Error.Message : null);
        Assert.Equal("2023-12-31", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM ReglesEligibilite WHERE Id = @id;", ("@id", premiere.Value)));
        Assert.Null(SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM ReglesEligibilite WHERE Id = @id;", ("@id", seconde.Value)));
    }

    [Fact]
    public async Task DefinirRegleAsync_commune_et_groupee_du_meme_critere_n_interferent_pas()
    {
        // GroupeId null (commune) et GroupeId='GE-TEST' sont des clés logiques
        // distinctes malgré le même (RubriqueId, CritereId) — définir l'une ne
        // doit pas fermer ou bloquer l'autre.
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        SeedGroupe(scope.Conn);
        var repo = new RegleEligibiliteRepository(scope.Conn);
        await repo.DefinirRegleAsync(
            "TEST_DNF", "GRADE", null, "=", "G-COMMUNE", "2020-01-01", null, DateTimeOffset.UtcNow);

        var groupee = await repo.DefinirRegleAsync(
            "TEST_DNF", "GRADE", "GE-TEST", "=", "G-GROUPE", "2020-01-01", null, DateTimeOffset.UtcNow);

        Assert.True(groupee.IsSuccess, groupee.IsFailure ? groupee.Error.Message : null);
        Assert.Null(SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM ReglesEligibilite WHERE GroupeId IS NULL;"));
        Assert.Null(SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM ReglesEligibilite WHERE GroupeId = 'GE-TEST';"));
    }

    [Fact]
    public async Task DefinirRegleAsync_date_effet_en_double_echoue_en_conflit()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new RegleEligibiliteRepository(scope.Conn);
        await repo.DefinirRegleAsync(
            "TEST_DNF", "GRADE", null, "=", "G-TEST", "2020-01-01", null, DateTimeOffset.UtcNow);

        var doublon = await repo.DefinirRegleAsync(
            "TEST_DNF", "GRADE", null, "=", "G-AUTRE", "2020-01-01", null, DateTimeOffset.UtcNow);

        Assert.True(doublon.IsFailure);
        Assert.Equal("conflict", doublon.Error.Code);
    }

    [Fact]
    public async Task DefinirRegleAsync_date_effet_non_posterieure_a_la_version_en_vigueur_echoue()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new RegleEligibiliteRepository(scope.Conn);
        await repo.DefinirRegleAsync(
            "TEST_DNF", "GRADE", null, "=", "G-TEST", "2020-01-01", null, DateTimeOffset.UtcNow);

        var retroactive = await repo.DefinirRegleAsync(
            "TEST_DNF", "GRADE", null, "=", "G-AUTRE", "2019-01-01", null, DateTimeOffset.UtcNow);

        Assert.True(retroactive.IsFailure);
        Assert.Equal("validation", retroactive.Error.Code);
    }

    [Fact]
    public async Task DefinirRegleAsync_rubrique_inconnue_echoue_avec_not_found()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new RegleEligibiliteRepository(scope.Conn);

        var result = await repo.DefinirRegleAsync(
            "INEXISTANTE", "GRADE", null, "=", "G-TEST", "2020-01-01", null, DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task DefinirRegleAsync_critere_inconnu_echoue_avec_not_found()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new RegleEligibiliteRepository(scope.Conn);

        var result = await repo.DefinirRegleAsync(
            "TEST_DNF", "CRITERE_INEXISTANT", null, "=", "G-TEST", "2020-01-01", null, DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task DefinirRegleAsync_groupe_inconnu_echoue_avec_not_found()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new RegleEligibiliteRepository(scope.Conn);

        var result = await repo.DefinirRegleAsync(
            "TEST_DNF", "GRADE", "GE-INEXISTANT", "=", "G-TEST", "2020-01-01", null, DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task DefinirRegleAsync_operateur_invalide_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new RegleEligibiliteRepository(scope.Conn);

        var result = await repo.DefinirRegleAsync(
            "TEST_DNF", "GRADE", null, "RESSEMBLE_A", "G-TEST", "2020-01-01", null, DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal(0, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM ReglesEligibilite;"));
    }

    [Fact]
    public async Task CloreRegleAsync_condition_ouverte_est_fermee_sans_remplacement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new RegleEligibiliteRepository(scope.Conn);
        var definie = await repo.DefinirRegleAsync(
            "TEST_DNF", "GRADE", null, "=", "G-TEST", "2020-01-01", null, DateTimeOffset.UtcNow);

        var result = await repo.CloreRegleAsync(definie.Value, "2025-12-31");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("2025-12-31", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM ReglesEligibilite WHERE Id = @id;", ("@id", definie.Value)));
    }

    [Fact]
    public async Task CloreRegleAsync_condition_deja_close_echoue_en_conflit()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new RegleEligibiliteRepository(scope.Conn);
        var definie = await repo.DefinirRegleAsync(
            "TEST_DNF", "GRADE", null, "=", "G-TEST", "2020-01-01", null, DateTimeOffset.UtcNow);
        await repo.CloreRegleAsync(definie.Value, "2025-12-31");

        var result = await repo.CloreRegleAsync(definie.Value, "2026-01-01");

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
    }

    [Fact]
    public async Task CloreRegleAsync_condition_inconnue_echoue_avec_not_found()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new RegleEligibiliteRepository(scope.Conn);

        var result = await repo.CloreRegleAsync("RE-INEXISTANTE", "2026-01-01");

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }
}
