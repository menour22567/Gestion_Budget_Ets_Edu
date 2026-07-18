using PaieEducation.Domain.Workbench.Constants;
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
}
