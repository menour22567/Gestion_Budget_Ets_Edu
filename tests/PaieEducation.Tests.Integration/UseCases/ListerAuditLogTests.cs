using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Workbench.Constants;
using PaieEducation.Infrastructure.Repositories.Workbench;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Preuve d'intégration de <see cref="ListerAuditLog"/> (Phase 6, tâche 4).
/// </summary>
public class ListerAuditLogTests
{
    [Fact]
    public async Task Executer_delegue_au_repository_et_renvoie_les_entrees()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new AuditLogRepository(scope.Conn);
        await repo.EnregistrerAsync(
            "admin", AuditActions.AppliquerEvolution, AuditEntityTypes.ValeurPoint, "VP-2026-01-01", null, null, DateTimeOffset.UtcNow);

        var useCase = new ListerAuditLog(repo);
        var result = await useCase.ExecuterAsync();

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Single(result.Value);
        Assert.Equal(AuditActions.AppliquerEvolution, result.Value[0].Action);
    }
}
