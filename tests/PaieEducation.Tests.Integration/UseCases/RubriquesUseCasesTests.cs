using Microsoft.Data.Sqlite;
using PaieEducation.Application.Referentiels.UseCases;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Tests des use cases C4.1 — écriture des rubriques &amp; formules :
/// <see cref="DefinirRubrique"/>, <see cref="DefinirFormuleRubrique"/>,
/// <see cref="DefinirParametreRubrique"/>. Les use cases orchestrent
/// <see cref="IRubriqueRepository"/> ; les formules invalides sont rejetées
/// avant persistance avec un message clair.
/// </summary>
public class RubriquesUseCasesTests
{
    private sealed class HorlogeFixe(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
        public DateTimeOffset UtcNow => now;
        public DateOnly Today => DateOnly.FromDateTime(now.UtcDateTime);
    }

    private static readonly HorlogeFixe Horloge = new(new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task DefinirRubrique_Executer_nominal()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var useCase = new DefinirRubrique(new RubriqueRepository(scope.Conn), Horloge);

        var result = await useCase.ExecuterAsync(new DefinirRubrique.Demande(
            "ISSRP_45", "Soutien scolaire 45%", "GAIN", "TRAITEMENT", "MENSUELLE", null, 10,
            true, true, "ISSRP groupe pédagogique élargi", true, false, null, null));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("ISSRP_45", result.Value);
    }

    [Fact]
    public async Task DefinirFormuleRubrique_Executer_formule_invalide_echoue()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new RubriqueRepository(scope.Conn);
        await repo.DefinirRubriqueAsync(
            "ISSRP_45", "Soutien scolaire 45%", "GAIN", "TRAITEMENT", "MENSUELLE", null, 10,
            true, true, "Desc", true, false, null, null, Horloge.UtcNow);
        var useCase = new DefinirFormuleRubrique(repo, Horloge);

        var result = await useCase.ExecuterAsync(new DefinirFormuleRubrique.Demande("ISSRP_45", "TBASE * * 0.45", "2026-01-01"));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task DefinirFormuleRubrique_Executer_nominal()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new RubriqueRepository(scope.Conn);
        await repo.DefinirRubriqueAsync(
            "ISSRP_45", "Soutien scolaire 45%", "GAIN", "TRAITEMENT", "MENSUELLE", null, 10,
            true, true, "Desc", true, false, null, null, Horloge.UtcNow);
        var useCase = new DefinirFormuleRubrique(repo, Horloge);

        var result = await useCase.ExecuterAsync(new DefinirFormuleRubrique.Demande("ISSRP_45", "TBASE * 0.45", "2026-01-01"));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("RF-ISSRP_45-2026-01-01", result.Value);
    }

    [Fact]
    public async Task DefinirParametreRubrique_Executer_nominal()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new RubriqueRepository(scope.Conn);
        await repo.DefinirRubriqueAsync(
            "MUNATEC", "Mutuelle MUNATEC", "RETENUE", "ASSIETTE_COTISABLE", "MENSUELLE", null, 90,
            false, false, "Mutuelle 1%", true, false, null, null, Horloge.UtcNow);
        var useCase = new DefinirParametreRubrique(repo, Horloge);

        var result = await useCase.ExecuterAsync(new DefinirParametreRubrique.Demande("MUNATEC", "TAUX", "1.0", "2008-01-01"));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("RP-MUNATEC-TAUX-2008-01-01", result.Value);
    }
}
