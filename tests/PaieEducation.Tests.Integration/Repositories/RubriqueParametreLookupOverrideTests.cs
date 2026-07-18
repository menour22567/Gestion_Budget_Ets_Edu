using Microsoft.Data.Sqlite;
using PaieEducation.Infrastructure.Repositories.Payroll;

namespace PaieEducation.Tests.Integration.Repositories;

/// <summary>
/// Tests d'intégration Lot 3.3 (J5N §2) — surcharge du port
/// <c>IRubriqueParametreLookup</c> par overrides « what-if » pour la
/// simulation d'évolution réglementaire. Cf. ADR-0007 D8.
/// </summary>
/// <remarks>
/// Lot 3.3 = lookup override UNIQUEMENT, sans intégration simulateur :
/// l'extension complète (D-P1, D-P2, D-P3) attendra un chantier qui durcira
/// le port pour passer le contexte de rubrique jusqu'au calculateur.
/// Ces tests verrouillent l'invariant minimal : override > DB.
/// </remarks>
public class RubriqueParametreLookupOverrideTests
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
    public async Task LireParametreAvecOverridesAsync_retourne_override_si_cle_presente_dans_overrides()
    {
        // C-P2 : override bat la DB. DB a 0.45, override a 0.50 → 0.50.
        using var scope = SchemaTestSupport.CreateMigrated();
        InsererParametre(scope.Conn, "ISSRP", "TAUX_45", "0.45", "2024-01-01");
        var lookup = new RubriqueParametreLookup(scope.Conn);
        var overrides = new Dictionary<string, decimal> { ["TAUX_45"] = 0.50m };

        var r = await lookup.LireParametreAvecOverridesAsync("TAUX_45", "2025-06-01", overrides);

        Assert.True(r.IsSuccess);
        Assert.Equal(0.50m, r.Value);  // override gagne, pas 0.45 (DB)
    }

    [Fact]
    public async Task LireParametreAvecOverridesAsync_retourne_valeur_DB_si_cle_absente_des_overrides()
    {
        // C-P3 : pas d'override pour la clé demandée → lecture DB normale.
        using var scope = SchemaTestSupport.CreateMigrated();
        InsererParametre(scope.Conn, "ISSRP", "TAUX_45", "0.45", "2024-01-01");
        var lookup = new RubriqueParametreLookup(scope.Conn);
        var overrides = new Dictionary<string, decimal> { ["AUTRE_CLE"] = 0.99m };

        var r = await lookup.LireParametreAvecOverridesAsync("TAUX_45", "2025-06-01", overrides);

        Assert.True(r.IsSuccess);
        Assert.Equal(0.45m, r.Value);  // DB lue normalement
    }

    [Fact]
    public async Task LireParametreAvecOverridesAsync_overrides_null_equivaut_a_LireParametreAsync()
    {
        // C-P4 : overrides = null ≡ pas d'override ≡ lecture DB.
        using var scope = SchemaTestSupport.CreateMigrated();
        InsererParametre(scope.Conn, "ISSRP", "TAUX_45", "0.45", "2024-01-01");
        var lookup = new RubriqueParametreLookup(scope.Conn);

        var avecNull = await lookup.LireParametreAvecOverridesAsync("TAUX_45", "2025-06-01", overrides: null);
        var sansOverride = await lookup.LireParametreAsync("TAUX_45", "2025-06-01");

        Assert.Equal(sansOverride.Value, avecNull.Value);
        Assert.Equal(0.45m, avecNull.Value);
    }

    [Fact]
    public async Task LireParametreAvecOverridesAsync_overrides_vide_equivaut_a_LireParametreAsync()
    {
        // C-P4bis : overrides vide (non null, mais 0 entrées) ≡ pas d'override.
        using var scope = SchemaTestSupport.CreateMigrated();
        InsererParametre(scope.Conn, "ISSRP", "TAUX_45", "0.45", "2024-01-01");
        var lookup = new RubriqueParametreLookup(scope.Conn);

        var avecVide = await lookup.LireParametreAvecOverridesAsync("TAUX_45", "2025-06-01", new Dictionary<string, decimal>());

        Assert.True(avecVide.IsSuccess);
        Assert.Equal(0.45m, avecVide.Value);
    }

    [Fact]
    public async Task LireParametreAvecOverridesAsync_retourne_NotFound_si_cle_absente_DB_et_pas_d_override()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var lookup = new RubriqueParametreLookup(scope.Conn);
        var overrides = new Dictionary<string, decimal> { ["AUTRE_CLE"] = 0.99m };

        var r = await lookup.LireParametreAvecOverridesAsync("INEXISTANT", "2025-06-01", overrides);

        Assert.True(r.IsFailure);
        Assert.Equal("not_found", r.Error.Code);
    }

    [Fact]
    public async Task LireParametreAvecOverridesAsync_override_preserve_la_resolution_point_in_time_pour_les_cles_non_overridees()
    {
        // Le C-P5 : si un override existe pour une clé mais pas pour une
        // autre, la lecture DB pour la clé non overridée respecte la
        // version (point-in-time). DB a 0.40 jusqu'au 2024-12-31 puis
        // 0.45. Override n'a que "TAUX_45" → la lecture de "TAUX_45"
        // utilise l'override, mais "TAUX_30" (non overridée) lit la
        // version en vigueur.
        using var scope = SchemaTestSupport.CreateMigrated();
        InsererParametre(scope.Conn, "ISSRP", "TAUX_45", "0.40", "2020-01-01", dateFin: "2024-12-31");
        InsererParametre(scope.Conn, "ISSRP", "TAUX_45", "0.45", "2025-01-01");
        InsererParametre(scope.Conn, "ISSRP", "TAUX_30", "0.30", "2020-01-01");
        var lookup = new RubriqueParametreLookup(scope.Conn);
        var overrides = new Dictionary<string, decimal> { ["TAUX_45"] = 0.50m };

        var taux45 = await lookup.LireParametreAvecOverridesAsync("TAUX_45", "2025-06-01", overrides);
        var taux30 = await lookup.LireParametreAvecOverridesAsync("TAUX_30", "2025-06-01", overrides);

        Assert.Equal(0.50m, taux45.Value);  // override
        Assert.Equal(0.30m, taux30.Value);  // DB, point-in-time
    }
}
