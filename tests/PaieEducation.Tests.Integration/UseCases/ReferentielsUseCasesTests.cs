using Microsoft.Data.Sqlite;
using PaieEducation.Application.Referentiels.UseCases;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Tests des 3 use cases GérerRéférentiels — grille indiciaire (Phase 5,
/// tâche 4, Q3) : <see cref="DefinirValeurPoint"/>,
/// <see cref="DefinirIndiceMinGrille"/>, <see cref="DefinirIndiceEchelon"/>.
/// </summary>
public class ReferentielsUseCasesTests
{
    private sealed class HorlogeFixe(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
        public DateTimeOffset UtcNow => now;
        public DateOnly Today => DateOnly.FromDateTime(now.UtcDateTime);
    }

    private static readonly HorlogeFixe Horloge = new(new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero));

    private static void SeedCategorieEtEchelon(SqliteConnection c) => SchemaTestSupport.Exec(c, """
        INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('13', 13, 'Catégorie 13', '2026-01-01T00:00:00Z', 'h');
        INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('5', 5, 'Échelon 5', '2026-01-01T00:00:00Z', 'h');
        """);

    [Fact]
    public async Task DefinirValeurPoint_Executer_nominal()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var useCase = new DefinirValeurPoint(new GrilleIndiciaireRepository(scope.Conn), Horloge);

        var result = await useCase.ExecuterAsync(new DefinirValeurPoint.Demande(45m, "2007-01-01", "2007"));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("VP-2007-01-01", result.Value);
    }

    [Fact]
    public async Task DefinirValeurPoint_Executer_valeur_negative_echoue()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var useCase = new DefinirValeurPoint(new GrilleIndiciaireRepository(scope.Conn), Horloge);

        var result = await useCase.ExecuterAsync(new DefinirValeurPoint.Demande(-1m, "2007-01-01", "2007"));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task DefinirIndiceMinGrille_Executer_nominal()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedCategorieEtEchelon(scope.Conn);
        var useCase = new DefinirIndiceMinGrille(new GrilleIndiciaireRepository(scope.Conn), Horloge);

        var result = await useCase.ExecuterAsync(new DefinirIndiceMinGrille.Demande("13", 578, "2024-01-01", "2024"));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("GI-13-2024-01-01", result.Value);
    }

    [Fact]
    public async Task DefinirIndiceEchelon_Executer_nominal()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedCategorieEtEchelon(scope.Conn);
        var useCase = new DefinirIndiceEchelon(new GrilleIndiciaireRepository(scope.Conn), Horloge);

        var result = await useCase.ExecuterAsync(new DefinirIndiceEchelon.Demande("5", 100, "2024-01-01", "2024"));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("IE-5-2024-01-01", result.Value);
    }

    [Fact]
    public async Task Ecriture_puis_lecture_VariableEngine_resout_l_ancienne_ou_la_nouvelle_valeur_selon_la_date()
    {
        // Preuve bout-en-bout : écrire via GérerRéférentiels puis relire via
        // VariableRepository (VariableEngine, tranche précédente) — les deux
        // sont cohérents sur le même schéma point-in-time.
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedCategorieEtEchelon(scope.Conn);

        var grille = new GrilleIndiciaireRepository(scope.Conn);
        await new DefinirValeurPoint(grille, Horloge).ExecuterAsync(new DefinirValeurPoint.Demande(45m, "2007-01-01", "2007"));
        await new DefinirIndiceMinGrille(grille, Horloge).ExecuterAsync(new DefinirIndiceMinGrille.Demande("13", 500, "2020-01-01", "v1"));
        await new DefinirIndiceMinGrille(grille, Horloge).ExecuterAsync(new DefinirIndiceMinGrille.Demande("13", 578, "2024-01-01", "v2"));
        await new DefinirIndiceEchelon(grille, Horloge).ExecuterAsync(new DefinirIndiceEchelon.Demande("5", 100, "2020-01-01", "v1"));

        var agent = new AgentContext(
            Filiere: null, Corps: null, Grade: null, Categorie: 13, Echelon: 5,
            AncienneteAnnees: null, Fonction: null, TypeContrat: null, TypeEtablissement: null,
            OrigineStatutaire: null, Note: null, ValeurPointIndiciaire: null,
            AssietteCotisable: null, AssietteImposable: null);

        var variables = new VariableRepository(scope.Conn);

        var avant = await variables.ResoudreAsync(agent, "2023-06-01");
        Assert.True(avant.IsSuccess, avant.IsFailure ? avant.Error.Message : null);
        Assert.Equal(500m, avant.Value["INDICE_MIN"]);

        var apres = await variables.ResoudreAsync(agent, "2025-06-01");
        Assert.True(apres.IsSuccess, apres.IsFailure ? apres.Error.Message : null);
        Assert.Equal(578m, apres.Value["INDICE_MIN"]);
    }
}
