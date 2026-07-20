using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Infrastructure.Repositories.Workbench;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Preuves d'intégration des use cases de l'éditeur DNF d'éligibilité
/// (chantier P6, audit du 19/07/2026) : <see cref="DefinirGroupeEligibilite"/>,
/// <see cref="CloreGroupeEligibilite"/>, <see cref="DefinirRegleEligibilite"/>,
/// <see cref="CloreRegleEligibilite"/>, <see cref="ListerCriteresEligibilite"/>.
/// Enveloppes minces — la logique de validation est déjà couverte par
/// <c>GroupeEligibiliteRepositoryTests</c>/<c>RegleEligibiliteRepositoryTests</c>;
/// ces tests prouvent la délégation, pas les règles.
/// </summary>
public class EligibiliteUseCasesTests
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
        VALUES ('TEST_DNF', 'Test DNF', 'GAIN', 'TBASE', 'MENSUELLE', 10, '2026-01-01T00:00:00Z', 'h');
        """);

    [Fact]
    public async Task DefinirGroupeEligibilite_nominal_delegue_au_repository()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var useCase = new DefinirGroupeEligibilite(new GroupeEligibiliteRepository(scope.Conn), Horloge);

        var result = await useCase.ExecuterAsync(new DefinirGroupeEligibilite.Demande(
            "GE-TEST", "TEST_DNF", "INFO", null, 100, "2026-01-01", null, "Décret X", "workbench"));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("GE-TEST", result.Value);
    }

    [Fact]
    public async Task DefinirGroupeEligibilite_rubrique_inconnue_echoue_sans_toucher_la_base()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var useCase = new DefinirGroupeEligibilite(new GroupeEligibiliteRepository(scope.Conn), Horloge);

        var result = await useCase.ExecuterAsync(new DefinirGroupeEligibilite.Demande(
            "GE-TEST", "INEXISTANTE", "INFO", null, 100, "2026-01-01", null, null, "workbench"));

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task CloreGroupeEligibilite_nominal_delegue_au_repository()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new GroupeEligibiliteRepository(scope.Conn);
        await new DefinirGroupeEligibilite(repo, Horloge).ExecuterAsync(new DefinirGroupeEligibilite.Demande(
            "GE-TEST", "TEST_DNF", "INFO", null, 100, "2026-01-01", null, null, "workbench"));
        var useCase = new CloreGroupeEligibilite(repo);

        var result = await useCase.ExecuterAsync(new CloreGroupeEligibilite.Demande("GE-TEST", "2026-12-31"));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("2026-12-31", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM GroupesEligibilite WHERE Id = 'GE-TEST';"));
    }

    [Fact]
    public async Task DefinirRegleEligibilite_nominal_delegue_au_repository()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var useCase = new DefinirRegleEligibilite(new RegleEligibiliteRepository(scope.Conn), Horloge);

        var result = await useCase.ExecuterAsync(new DefinirRegleEligibilite.Demande(
            "TEST_DNF", "GRADE", null, "=", "G-TEST", "2026-01-01", "Décret X"));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("G-TEST", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT Valeur FROM ReglesEligibilite WHERE Id = @id;", ("@id", result.Value)));
    }

    [Fact]
    public async Task DefinirRegleEligibilite_critere_inconnu_echoue_sans_toucher_la_base()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var useCase = new DefinirRegleEligibilite(new RegleEligibiliteRepository(scope.Conn), Horloge);

        var result = await useCase.ExecuterAsync(new DefinirRegleEligibilite.Demande(
            "TEST_DNF", "CRITERE_INEXISTANT", null, "=", "G-TEST", "2026-01-01"));

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task CloreRegleEligibilite_nominal_delegue_au_repository()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubrique(scope.Conn);
        var repo = new RegleEligibiliteRepository(scope.Conn);
        var definie = await new DefinirRegleEligibilite(repo, Horloge).ExecuterAsync(new DefinirRegleEligibilite.Demande(
            "TEST_DNF", "GRADE", null, "=", "G-TEST", "2026-01-01"));
        var useCase = new CloreRegleEligibilite(repo);

        var result = await useCase.ExecuterAsync(new CloreRegleEligibilite.Demande(definie.Value, "2026-12-31"));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("2026-12-31", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM ReglesEligibilite WHERE Id = @id;", ("@id", definie.Value)));
    }

    [Fact]
    public async Task ListerCriteresEligibilite_renvoie_les_criteres_seedes_par_V009()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var useCase = new ListerCriteresEligibilite(new WorkbenchReadRepository(scope.Conn));

        var result = await useCase.ExecuterAsync();

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value, c => c.Id == "GRADE");
        Assert.Contains(result.Value, c => c.Id == "ORIGINE_STATUTAIRE");
        Assert.Equal(10, result.Value.Count);
    }
}
