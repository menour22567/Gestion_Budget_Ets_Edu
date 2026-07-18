using Microsoft.Data.Sqlite;
using PaieEducation.Application.Referentiels.UseCases;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Integration.Repositories;

/// <summary>
/// Tests de <see cref="RubriqueRepository"/> (chantier C4.1 — écriture des
/// rubriques &amp; formules) : création/édition d'une rubrique, définition
/// d'une formule versionnée (validation par <c>FormulaParser</c>), paramètre
/// versionné et refus de cycle dans le graphe DAG des dépendances.
/// </summary>
public class RubriqueRepositoryTests
{
    private sealed class HorlogeFixe(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
        public DateTimeOffset UtcNow => now;
        public DateOnly Today => DateOnly.FromDateTime(now.UtcDateTime);
    }

    private static readonly HorlogeFixe Horloge = new(new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero));

    private static async Task<string> CreerRubriqueAsync(SqliteConnection c, string id)
    {
        var repo = new RubriqueRepository(c);
        var result = await repo.DefinirRubriqueAsync(
            id, "Libellé " + id, "GAIN", "TRAITEMENT", "MENSUELLE", null, 10,
            true, true, "Desc", true, false, null, null, Horloge.UtcNow);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        return result.Value;
    }

    [Fact]
    public async Task DefinirRubriqueAsync_cree_puis_met_a_jour_sans_erreur()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new RubriqueRepository(scope.Conn);

        var cree = await repo.DefinirRubriqueAsync(
            "QUALIF", "Indemnité de qualification", "GAIN", "TRAITEMENT", "MENSUELLE", null, 10,
            true, true, "Barème par catégorie", true, false, null, null, Horloge.UtcNow);
        Assert.True(cree.IsSuccess, cree.IsFailure ? cree.Error.Message : null);
        Assert.Equal("QUALIF", cree.Value);

        var maj = await repo.DefinirRubriqueAsync(
            "QUALIF", "Indemnité de qualification (MAJ)", "GAIN", "TRAITEMENT", "MENSUELLE", null, 11,
            true, true, "Barème par catégorie", true, false, null, null, Horloge.UtcNow);
        Assert.True(maj.IsSuccess, maj.IsFailure ? maj.Error.Message : null);

        Assert.Equal("Indemnité de qualification (MAJ)",
            SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT Libelle FROM Rubriques WHERE Id='QUALIF';"));
    }

    [Fact]
    public async Task DefinirRubriqueAsync_nature_invalide_echoue_en_validation()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new RubriqueRepository(scope.Conn);

        var result = await repo.DefinirRubriqueAsync(
            "X", "X", "TOKEN_INVALIDE", "TRAITEMENT", "MENSUELLE", null, 10,
            true, true, "Desc", true, false, null, null, Horloge.UtcNow);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task DefinirFormuleAsync_formule_invalide_rejetee_sans_exception()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await CreerRubriqueAsync(scope.Conn, "ISSRP_45");
        var repo = new RubriqueRepository(scope.Conn);

        var result = await repo.DefinirFormuleAsync("ISSRP_45", "TBASE * * 0.45", "2026-01-01", 0, null, Horloge.UtcNow);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task DefinirFormuleAsync_formule_valide_persistee_et_versionnee()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await CreerRubriqueAsync(scope.Conn, "ISSRP_45");
        var repo = new RubriqueRepository(scope.Conn);

        var premiere = await repo.DefinirFormuleAsync("ISSRP_45", "TBASE * 0.45", "2026-01-01", 0, null, Horloge.UtcNow);
        Assert.True(premiere.IsSuccess, premiere.IsFailure ? premiere.Error.Message : null);
        Assert.Equal("RF-ISSRP_45-2026-01-01", premiere.Value);

        var seconde = await repo.DefinirFormuleAsync("ISSRP_45", "TBASE * 0.50", "2027-01-01", 0, null, Horloge.UtcNow);
        Assert.True(seconde.IsSuccess, seconde.IsFailure ? seconde.Error.Message : null);

        Assert.Equal("2026-12-31", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM RubriqueFormules WHERE Id=@id;", ("@id", premiere.Value)));
        Assert.Equal("TBASE * 0.50", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT Expression FROM RubriqueFormules WHERE Id=@id;", ("@id", seconde.Value)));
    }

    [Fact]
    public async Task DefinirParametreAsync_persistee_et_versionnee()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await CreerRubriqueAsync(scope.Conn, "MUNATEC");
        var repo = new RubriqueRepository(scope.Conn);

        var premiere = await repo.DefinirParametreAsync("MUNATEC", "TAUX", "1.0", "2008-01-01", null, Horloge.UtcNow);
        Assert.True(premiere.IsSuccess, premiere.IsFailure ? premiere.Error.Message : null);

        var seconde = await repo.DefinirParametreAsync("MUNATEC", "TAUX", "1.2", "2026-01-01", "Décret", Horloge.UtcNow);
        Assert.True(seconde.IsSuccess, seconde.IsFailure ? seconde.Error.Message : null);

        Assert.Equal("1.2", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT Valeur FROM RubriqueParametres WHERE Id=@id;", ("@id", seconde.Value)));
    }

    [Fact]
    public async Task DefinirDependanceAsync_cycle_refuse()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await CreerRubriqueAsync(scope.Conn, "RUB_A");
        await CreerRubriqueAsync(scope.Conn, "RUB_B");
        var repo = new RubriqueRepository(scope.Conn);

        var ok = await repo.DefinirDependanceAsync("RUB_A", "RUB_B", "2026-01-01", null, Horloge.UtcNow);
        Assert.True(ok.IsSuccess, ok.IsFailure ? ok.Error.Message : null);

        var cycle = await repo.DefinirDependanceAsync("RUB_B", "RUB_A", "2026-01-01", null, Horloge.UtcNow);
        Assert.True(cycle.IsFailure);
    }
}
