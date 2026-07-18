using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Shared.Results;

namespace PaieEducation.Tests.Integration.Repositories;

/// <summary>
/// Tests d'intégration Lot 1.1 — verrouillage des paramètres système critiques.
/// Le principe "paramétrage en base, zero hardcoding" impose qu'un paramètre
/// absent ou corrompu provoque un échec explicite exploitable par l'UI, et
/// non un fallback silencieux qui masquerait une mauvaise configuration.
///
/// Couvre:
///   - <c>LireModeArrondiAsync</c> : strict (NotFound / Validation) — Lot 1.1
///   - <c>LireDecimalObligatoireAsync</c> : strict — non-régression
///   - <c>LireDecimalOuDefautAsync</c> : helper paramètre non-critique — renommé
///     depuis <c>LireDecimalAsync</c> (Option B1 du plan) pour expliciter le
///     contrat "valeur par défaut acceptée" et éviter les abus futurs.
/// </summary>
public class ParametreSystemeRepositoryTests
{
    private const string Creer = "2026-01-01T00:00:00Z";

    // ---------------------------------------------------------------------
    // LireModeArrondiAsync — strict (Lot 1.1)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task LireModeArrondiAsync_retourne_NotFound_si_cle_absente()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new ParametreSystemeRepository(scope.Conn);

        var result = await repo.LireModeArrondiAsync("2025-06-01");

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Contains("ARRONDI_MODE", result.Error.Message);
    }

    [Fact]
    public async Task LireModeArrondiAsync_retourne_Validation_si_valeur_inconnue()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsererParametre(scope.Conn, "ARRONDI_MODE", "FOO_BAR", "TEXT", "2024-01-01");
        var repo = new ParametreSystemeRepository(scope.Conn);

        var result = await repo.LireModeArrondiAsync("2025-06-01");

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
    }

    [Fact]
    public async Task LireModeArrondiAsync_retourne_la_valeur_pour_date_si_versionnee()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsererParametre(scope.Conn, "ARRONDI_MODE", "DIZAINE", "TEXT", "2024-01-01");
        var repo = new ParametreSystemeRepository(scope.Conn);

        var result = await repo.LireModeArrondiAsync("2025-06-01");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal(ModeArrondi.Dizaine, result.Value);
    }

    [Fact]
    public async Task LireModeArrondiAsync_retourne_NotFound_si_date_anterieure_a_la_premiere_version()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsererParametre(scope.Conn, "ARRONDI_MODE", "DIZAINE", "TEXT", "2024-01-01");
        var repo = new ParametreSystemeRepository(scope.Conn);

        var result = await repo.LireModeArrondiAsync("2000-01-01");

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    // ---------------------------------------------------------------------
    // LireDecimalObligatoireAsync — strict (non-régression)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task LireDecimalObligatoireAsync_retourne_NotFound_si_absent()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new ParametreSystemeRepository(scope.Conn);

        var result = await repo.LireDecimalObligatoireAsync("BASE_PAPP", "2025-06-01");

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Contains("BASE_PAPP", result.Error.Message);
    }

    [Fact]
    public async Task LireDecimalObligatoireAsync_retourne_Validation_si_valeur_non_decimale()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsererParametre(scope.Conn, "BASE_PAPP", "pas_un_nombre", "REAL", "2024-01-01");
        var repo = new ParametreSystemeRepository(scope.Conn);

        var result = await repo.LireDecimalObligatoireAsync("BASE_PAPP", "2025-06-01");

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
    }

    [Fact]
    public async Task LireDecimalObligatoireAsync_retourne_la_valeur_pour_date_si_versionnee()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsererParametre(scope.Conn, "BASE_PAPP", "0.40", "REAL", "2024-01-01");
        var repo = new ParametreSystemeRepository(scope.Conn);

        var result = await repo.LireDecimalObligatoireAsync("BASE_PAPP", "2025-06-01");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal(0.40m, result.Value);
    }

    [Fact]
    public async Task LireDecimalObligatoireAsync_retourne_NotFound_si_date_anterieure_a_la_premiere_version()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsererParametre(scope.Conn, "BASE_PAPP", "0.40", "REAL", "2024-01-01");
        var repo = new ParametreSystemeRepository(scope.Conn);

        var result = await repo.LireDecimalObligatoireAsync("BASE_PAPP", "2000-01-01");

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    // ---------------------------------------------------------------------
    // LireDecimalOuDefautAsync — helper "paramètre non-critique" (renommé B1)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task LireDecimalOuDefautAsync_retourne_defaut_si_absent()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new ParametreSystemeRepository(scope.Conn);

        var result = await repo.LireDecimalOuDefautAsync("VALEUR_POINT_DEFAUT", 45m, "2025-06-01");

        Assert.True(result.IsSuccess);
        Assert.Equal(45m, result.Value);
    }

    [Fact]
    public async Task LireDecimalOuDefautAsync_retourne_defaut_si_valeur_non_decimale()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsererParametre(scope.Conn, "VALEUR_POINT_DEFAUT", "corrompu", "REAL", "2024-01-01");
        var repo = new ParametreSystemeRepository(scope.Conn);

        var result = await repo.LireDecimalOuDefautAsync("VALEUR_POINT_DEFAUT", 45m, "2025-06-01");

        Assert.True(result.IsSuccess);
        Assert.Equal(45m, result.Value);
    }

    [Fact]
    public async Task LireDecimalOuDefautAsync_retourne_la_valeur_si_trouvee()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsererParametre(scope.Conn, "VALEUR_POINT_DEFAUT", "50.25", "REAL", "2024-01-01");
        var repo = new ParametreSystemeRepository(scope.Conn);

        var result = await repo.LireDecimalOuDefautAsync("VALEUR_POINT_DEFAUT", 45m, "2025-06-01");

        Assert.True(result.IsSuccess);
        Assert.Equal(50.25m, result.Value);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static void InsererParametre(SqliteConnection c, string cle, string valeur, string type, string dateEffet)
    {
        SchemaTestSupport.Exec(c, """
            INSERT INTO Parametres (Id, Cle, Valeur, Type, Description, DateEffet, DateFin, Source, Hash, CreatedAt)
            VALUES ($id, $cle, $valeur, $type, $desc, $dateEffet, NULL, $source, $hash, $creer);
            """,
            ("$id", $"P-{cle}-{dateEffet}"),
            ("$cle", cle),
            ("$valeur", valeur),
            ("$type", type),
            ("$desc", $"Test {cle}"),
            ("$dateEffet", dateEffet),
            ("$source", "test"),
            ("$hash", "h"),
            ("$creer", Creer));
    }
}
