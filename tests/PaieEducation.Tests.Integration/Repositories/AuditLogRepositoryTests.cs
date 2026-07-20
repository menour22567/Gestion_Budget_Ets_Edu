using PaieEducation.Domain.Workbench.Constants;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Infrastructure.Repositories.Workbench;

namespace PaieEducation.Tests.Integration.Repositories;

/// <summary>
/// Tests de <see cref="AuditLogRepository"/> (Phase 5, tâche 5, D8) : première
/// écriture applicative de la table <c>AuditLog</c> (V001, jamais câblée avant
/// cette tranche).
/// </summary>
public class AuditLogRepositoryTests
{
    [Fact]
    public async Task EnregistrerAsync_persiste_une_ligne_avec_les_bonnes_colonnes()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new AuditLogRepository(scope.Conn);

        var result = await repo.EnregistrerAsync(
            actor: "admin", action: AuditActions.AppliquerEvolution, entityType: AuditEntityTypes.ValeurPoint, entityId: "VP-2026-01-01",
            payload: """{"description":"test"}""", comment: "Décret X",
            occurredAt: new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);

        Assert.Equal(1, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM AuditLog;"));
        Assert.Equal("admin", SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT Actor FROM AuditLog;"));
        Assert.Equal(AuditActions.AppliquerEvolution, SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT Action FROM AuditLog;"));
        Assert.Equal(AuditEntityTypes.ValeurPoint, SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT EntityType FROM AuditLog;"));
        Assert.Equal("VP-2026-01-01", SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT EntityId FROM AuditLog;"));
        Assert.Contains("test", SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT Payload FROM AuditLog;"));
        Assert.Equal("Décret X", SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT Comment FROM AuditLog;"));
    }

    [Fact]
    public async Task EnregistrerAsync_accepte_entityId_et_payload_nuls()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new AuditLogRepository(scope.Conn);

        var result = await repo.EnregistrerAsync(
            actor: "job", action: AuditActions.Calcul, entityType: AuditEntityTypes.Bulletin, entityId: null, payload: null, comment: null,
            occurredAt: DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Null(SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT EntityId FROM AuditLog;"));
    }

    [Fact]
    public async Task ListerAsync_renvoie_les_entrees_les_plus_recentes_en_premier()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new AuditLogRepository(scope.Conn);
        await repo.EnregistrerAsync(
            "admin", AuditActions.AppliquerEvolution, AuditEntityTypes.ValeurPoint, "VP-2020-01-01", null, null,
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await repo.EnregistrerAsync(
            "admin", AuditActions.AppliquerEvolutionBypass, AuditEntityTypes.ValeurPoint, "VP-2026-01-01", null, "urgence",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var result = await repo.ListerAsync();

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal(AuditActions.AppliquerEvolutionBypass, result.Value[0].Action);
        Assert.Equal(AuditActions.AppliquerEvolution, result.Value[1].Action);
    }

    [Fact]
    public async Task ListerAsync_sur_base_vide_renvoie_une_liste_vide()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new AuditLogRepository(scope.Conn);

        var result = await repo.ListerAsync();

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Empty(result.Value);
    }

    private static async Task<AuditLogRepository> SeedTroisEntreesAsync(Microsoft.Data.Sqlite.SqliteConnection conn)
    {
        var repo = new AuditLogRepository(conn);
        await repo.EnregistrerAsync(
            "admin", AuditActions.AppliquerEvolution, AuditEntityTypes.ValeurPoint, "VP-1", null, null,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await repo.EnregistrerAsync(
            "job", AuditActions.Calcul, AuditEntityTypes.Bulletin, "BUL-1", null, null,
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        await repo.EnregistrerAsync(
            "admin", AuditActions.AppliquerEvolutionBypass, AuditEntityTypes.ValeurPoint, "VP-2", null, "urgence",
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
        return repo;
    }

    [Fact]
    public async Task ListerAsync_filtre_par_acteur()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = await SeedTroisEntreesAsync(scope.Conn);

        var result = await repo.ListerAsync(new FiltreAuditLog(Actor: "job"));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Single(result.Value);
        Assert.Equal("job", result.Value[0].Actor);
    }

    [Fact]
    public async Task ListerAsync_filtre_par_action()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = await SeedTroisEntreesAsync(scope.Conn);

        var result = await repo.ListerAsync(new FiltreAuditLog(Action: AuditActions.AppliquerEvolutionBypass));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Single(result.Value);
        Assert.Equal("VP-2", result.Value[0].EntityId);
    }

    [Fact]
    public async Task ListerAsync_filtre_par_type_entite()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = await SeedTroisEntreesAsync(scope.Conn);

        var result = await repo.ListerAsync(new FiltreAuditLog(EntityType: AuditEntityTypes.Bulletin));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Single(result.Value);
        Assert.Equal(AuditEntityTypes.Bulletin, result.Value[0].EntityType);
    }

    [Fact]
    public async Task ListerAsync_filtre_par_periode()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = await SeedTroisEntreesAsync(scope.Conn);

        var result = await repo.ListerAsync(new FiltreAuditLog(
            DateDebut: new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            DateFin: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Single(result.Value);
        Assert.Equal("BUL-1", result.Value[0].EntityId);
    }

    [Fact]
    public async Task ListerAsync_filtres_combines_en_ET_sans_resultat()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = await SeedTroisEntreesAsync(scope.Conn);

        var result = await repo.ListerAsync(new FiltreAuditLog(Actor: "job", EntityType: AuditEntityTypes.ValeurPoint));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ListerAsync_pagine_avec_tri_deterministe()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = await SeedTroisEntreesAsync(scope.Conn);

        var page1 = await repo.ListerAsync(new FiltreAuditLog(Page: 1, TaillePage: 2));
        var page2 = await repo.ListerAsync(new FiltreAuditLog(Page: 2, TaillePage: 2));

        Assert.True(page1.IsSuccess, page1.IsFailure ? page1.Error.Message : null);
        Assert.True(page2.IsSuccess, page2.IsFailure ? page2.Error.Message : null);
        Assert.Equal(2, page1.Value.Count);
        Assert.Single(page2.Value);
        Assert.Equal("VP-2", page1.Value[0].EntityId); // plus récent d'abord
        Assert.Equal("BUL-1", page1.Value[1].EntityId);
        Assert.Equal("VP-1", page2.Value[0].EntityId); // plus ancien, page 2
    }

    [Fact]
    public async Task ListerAsync_page_invalide_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new AuditLogRepository(scope.Conn);

        var result = await repo.ListerAsync(new FiltreAuditLog(Page: 0));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ListerAsync_taille_page_hors_bornes_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new AuditLogRepository(scope.Conn);

        var tropGrande = await repo.ListerAsync(new FiltreAuditLog(TaillePage: FiltreAuditLog.TaillePageMax + 1));
        var nulle = await repo.ListerAsync(new FiltreAuditLog(TaillePage: 0));

        Assert.True(tropGrande.IsFailure);
        Assert.True(nulle.IsFailure);
    }
}
