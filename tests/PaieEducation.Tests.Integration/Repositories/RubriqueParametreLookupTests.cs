using Microsoft.Data.Sqlite;
using PaieEducation.Infrastructure.Repositories.Payroll;

namespace PaieEducation.Tests.Integration.Repositories;

/// <summary>
/// Tests d'intégration Lot 1.2 — port <c>IRubriqueParametreLookup</c>
/// (lecture de la table <c>RubriqueParametres</c> à la date d'effet).
/// Sert au calculateur <c>CONSTANTE_REGLEMENTAIRE</c> pour résoudre un
/// taux/plafond/borne réglementaire depuis la base (zéro hardcoding).
/// </summary>
public class RubriqueParametreLookupTests
{
    private const string Creer = "2026-01-01T00:00:00Z";

    private static void InsererParametre(
        SqliteConnection c, string rubriqueId, string cle, string valeur, string dateEffet,
        string? dateFin = null)
    {
        var id = $"RP-{rubriqueId}-{cle}-{dateEffet}".Replace(":", "-");
        SchemaTestSupport.Exec(c, """
            INSERT INTO Rubriques (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul,
                                   EstImposable, EstCotisable, EstAffectableManuellement, OccurrencesMultiples,
                                   Description, CreatedAt, Hash)
            VALUES ($rid, $rlib, 'GAIN', 'TBASE', 'MENSUELLE', 1, 1, 1, 0, 0, 'Test', $creer, 'h')
            ON CONFLICT(Id) DO NOTHING;
            """, ("$rid", rubriqueId), ("$rlib", $"Rubrique {rubriqueId}"), ("$creer", Creer));

        SchemaTestSupport.Exec(c, """
            INSERT INTO RubriqueParametres
                (Id, RubriqueId, Cle, Valeur, DateEffet, DateFin, Source, Hash, CreatedAt)
            VALUES ($id, $rid, $cle, $valeur, $de, $df, 'test', 'h', $creer);
            """,
            ("$id", id), ("$rid", rubriqueId), ("$cle", cle), ("$valeur", valeur),
            ("$de", dateEffet), ("$df", dateFin ?? (object)DBNull.Value), ("$creer", Creer));
    }

    [Fact]
    public async Task LireParametreAsync_retourne_la_valeur_a_la_date_si_versionnee()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsererParametre(scope.Conn, "ISSRP", "TAUX_45", "0.45", "2024-01-01");
        var lookup = new RubriqueParametreLookup(scope.Conn);

        var result = await lookup.LireParametreAsync("TAUX_45", "2025-06-01");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal(0.45m, result.Value);
    }

    [Fact]
    public async Task LireParametreAsync_retourne_NotFound_si_cle_absente()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var lookup = new RubriqueParametreLookup(scope.Conn);

        var result = await lookup.LireParametreAsync("INEXISTANT", "2025-06-01");

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task LireParametreAsync_retourne_NotFound_si_date_anterieure_a_la_premiere_version()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsererParametre(scope.Conn, "ISSRP", "TAUX_45", "0.45", "2024-01-01");
        var lookup = new RubriqueParametreLookup(scope.Conn);

        var result = await lookup.LireParametreAsync("TAUX_45", "2000-01-01");

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task LireParametreAsync_retourne_Validation_si_valeur_non_decimale()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsererParametre(scope.Conn, "ISSRP", "TAUX_45", "pas_un_nombre", "2024-01-01");
        var lookup = new RubriqueParametreLookup(scope.Conn);

        var result = await lookup.LireParametreAsync("TAUX_45", "2025-06-01");

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
    }

    [Fact]
    public async Task LireParametreAsync_cherche_a_la_date_fermee_si_DateFin_renseignee()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsererParametre(scope.Conn, "ISSRP", "TAUX_45", "0.45", "2024-01-01", dateFin: "2024-12-31");
        var lookup = new RubriqueParametreLookup(scope.Conn);

        var dansLaVersion = await lookup.LireParametreAsync("TAUX_45", "2024-06-01");
        var apresLaVersion = await lookup.LireParametreAsync("TAUX_45", "2025-06-01");

        Assert.True(dansLaVersion.IsSuccess);
        Assert.Equal(0.45m, dansLaVersion.Value);
        Assert.True(apresLaVersion.IsFailure);
        Assert.Equal("not_found", apresLaVersion.Error.Code);
    }
}
