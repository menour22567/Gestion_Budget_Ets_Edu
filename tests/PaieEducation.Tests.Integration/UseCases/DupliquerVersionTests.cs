using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Preuve d'intégration de <see cref="DupliquerVersion"/> (Phase 5, tâche 5,
/// mode « Duplication » J3I §7.4) — portée limitée à <c>ValeurPoint</c>.
/// </summary>
public class DupliquerVersionTests
{
    private sealed class HorlogeFixe(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
        public DateTimeOffset UtcNow => now;
        public DateOnly Today => DateOnly.FromDateTime(now.UtcDateTime);
    }

    private static readonly HorlogeFixe Horloge = new(new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Executer_clone_la_valeur_du_point_en_vigueur_vers_une_nouvelle_periode()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var grille = new GrilleIndiciaireRepository(scope.Conn);
        await grille.DefinirValeurPointAsync(45m, "2007-01-01", "2007", null, DateTimeOffset.UtcNow);

        var useCase = new DupliquerVersion(grille, Horloge);
        var result = await useCase.ExecuterAsync(new DupliquerVersion.Demande("2026-01-01", "2026", "Décret X"));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("VP-2026-01-01", result.Value);
        Assert.Equal(45.0, SchemaTestSupport.Scalar<double>(
            scope.Conn, "SELECT Valeur FROM ValeurPoint WHERE Id = @id;", ("@id", result.Value)));
    }

    [Fact]
    public async Task Executer_sans_version_en_vigueur_echoue_avec_not_found()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var useCase = new DupliquerVersion(new GrilleIndiciaireRepository(scope.Conn), Horloge);

        var result = await useCase.ExecuterAsync(new DupliquerVersion.Demande("2026-01-01", "2026"));

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }
}
