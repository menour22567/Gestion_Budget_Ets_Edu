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
            actor: "admin", action: "APPLIQUER_EVOLUTION", entityType: "ValeurPoint", entityId: "VP-2026-01-01",
            payload: """{"description":"test"}""", comment: "Décret X",
            occurredAt: new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);

        Assert.Equal(1, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM AuditLog;"));
        Assert.Equal("admin", SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT Actor FROM AuditLog;"));
        Assert.Equal("APPLIQUER_EVOLUTION", SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT Action FROM AuditLog;"));
        Assert.Equal("ValeurPoint", SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT EntityType FROM AuditLog;"));
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
            actor: "job", action: "CALCUL", entityType: "Bulletin", entityId: null, payload: null, comment: null,
            occurredAt: DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Null(SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT EntityId FROM AuditLog;"));
    }
}
