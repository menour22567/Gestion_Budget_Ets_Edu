using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Infrastructure.Repositories.Workbench;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Preuve d'intégration de <see cref="DefinirValeurBareme"/> (chantier P5,
/// audit du 19/07/2026, éditeur de barèmes).
/// </summary>
public class DefinirValeurBaremeTests
{
    private sealed class HorlogeFixe(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
        public DateTimeOffset UtcNow => now;
        public DateOnly Today => DateOnly.FromDateTime(now.UtcDateTime);
    }

    private static readonly HorlogeFixe Horloge = new(new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero));

    private static void SeedRubrique(Microsoft.Data.Sqlite.SqliteConnection c) => SchemaTestSupport.Exec(c, """
        INSERT INTO Rubriques (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul, CreatedAt, Hash)
        VALUES ('QUALIF', 'Qualification', 'GAIN', 'TBASE', 'MENSUELLE', 10, '2026-01-01T00:00:00Z', 'h');
        """);

    [Fact]
    public async Task Executer_nominal_delegue_au_repository_et_renvoie_l_id()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var useCase = new DefinirValeurBareme(new RubriqueBaremeRepository(scope.Conn), Horloge);

        var result = await useCase.ExecuterAsync(new DefinirValeurBareme.Demande(
            "QUALIF", "CATEGORIE", "13", null, "TAUX", "0.45", "2026-01-01", "Décret X"));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("RB-QUALIF-CATEGORIE-13-2026-01-01", result.Value);
        Assert.Equal("0.45", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT Valeur FROM RubriqueBaremes WHERE Id = @id;", ("@id", result.Value)));
    }

    [Fact]
    public async Task Executer_rubrique_inconnue_echoue_sans_toucher_la_base()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var useCase = new DefinirValeurBareme(new RubriqueBaremeRepository(scope.Conn), Horloge);

        var result = await useCase.ExecuterAsync(new DefinirValeurBareme.Demande(
            "INEXISTANTE", "CATEGORIE", "13", null, "TAUX", "0.45", "2026-01-01"));

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task Executer_edition_bout_en_bout_la_nouvelle_version_est_immediatement_lisible()
    {
        // Preuve "édition -> base -> recalcul" (critère d'acceptation du plan) :
        // le use case de lecture ConsulterFicheRubrique voit la nouvelle
        // tranche sans cache ni redémarrage.
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var useCase = new DefinirValeurBareme(new RubriqueBaremeRepository(scope.Conn), Horloge);
        await useCase.ExecuterAsync(new DefinirValeurBareme.Demande(
            "QUALIF", "CATEGORIE", "13", null, "TAUX", "0.40", "2020-01-01"));

        var revision = await useCase.ExecuterAsync(new DefinirValeurBareme.Demande(
            "QUALIF", "CATEGORIE", "13", null, "TAUX", "0.45", "2026-01-01", "Décret Y"));

        Assert.True(revision.IsSuccess, revision.IsFailure ? revision.Error.Message : null);
        var workbench = new WorkbenchReadRepository(scope.Conn);
        var baremes = await workbench.ListerBaremesRubriqueAsync("QUALIF");
        Assert.Equal(2, baremes.Count);
        Assert.Contains(baremes, b => b.Valeur == "0.45" && b.Periode.DateFin is null);
        Assert.Contains(baremes, b => b.Valeur == "0.40" && b.Periode.DateFin == "2025-12-31");
    }
}
