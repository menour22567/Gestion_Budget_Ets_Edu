using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PaieEducation.Application.Agents.UseCases;
using PaieEducation.Application.DependencyInjection;
using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Application.Referentiels.UseCases;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Infrastructure.DependencyInjection;
using PaieEducation.Shared.Time;
using PaieEducation.Tools.Seeding;

namespace PaieEducation.Tests.Integration.DependencyInjection;

/// <summary>
/// Preuve du Composition Root (Phase 5, tâche 3) : le graphe DI
/// (<c>AddApplication</c> + <c>AddInfrastructure</c>) se résout et produit
/// le même résultat que les tests précédents qui instanciaient les classes
/// à la main — pas seulement qu'il compile.
/// </summary>
public class CompositionRootTests
{
    private static void SeedAgentReel(SqliteConnection conn)
    {
        Exec(conn, """
            INSERT INTO Filieres (Id, Libelle, CreatedAt, Hash) VALUES ('ENSEIGNANT', 'Enseignant', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ('PDLP', 'Prof. École primaire', 'ENSEIGNANT', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('PDLP-G105', 'Professeur de l''Ecole primaire', 'PDLP', 1, '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('13', 13, 'Catégorie 13', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('5', 5, 'Échelon 5', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO ValeurPoint (Id, DateEffet, Valeur, Version, Hash, CreatedAt) VALUES ('VP-PILOTE', '2007-01-01', 45, 'v', 'h', '2026-01-01T00:00:00Z');
            INSERT INTO GrilleIndiciaire (Id, CategorieId, DateEffet, IndiceMin, Version, Hash, CreatedAt) VALUES ('GI-PILOTE', '13', '2020-01-01', 578, 'v', 'h', '2026-01-01T00:00:00Z');
            INSERT INTO IndicesEchelon (Id, EchelonId, DateEffet, Indice, Version, Hash, CreatedAt) VALUES ('IE-PILOTE', '5', '2020-01-01', 100, 'v', 'h', '2026-01-01T00:00:00Z');
            INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, CreatedAt)
                VALUES ('A-PILOTE', 'MAT-PILOTE', 'Test', 'Pilote', '1985-01-01', '2010-09-01', 'M', '2026-01-01T00:00:00Z');
            INSERT INTO Carrieres (Id, AgentId, GradeId, CategorieId, EchelonId, TypeContrat, DateEffet, Motif, CreatedAt)
                VALUES ('C-PILOTE', 'A-PILOTE', 'PDLP-G105', '13', '5', 'STATUTAIRE', '2010-09-01', 'Recrutement', '2026-01-01T00:00:00Z');
            INSERT INTO AgentAttributs (Id, AgentId, Attribut, Valeur, DateEffet, CreatedAt)
                VALUES ('AA-PILOTE', 'A-PILOTE', 'ORIGINE_STATUTAIRE', 'ENSEIGNANT', '2010-09-01', '2026-01-01T00:00:00Z');
            """);
    }

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task Le_conteneur_resout_et_execute_CalculerBulletin_bout_en_bout()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await new ReglementaireSeeder().SeedAsync(scope.Conn);
        await new IrgSeeder().SeedAsync(scope.Conn);
        await new FormulesSeeder().SeedAsync(scope.Conn);
        SeedAgentReel(scope.Conn);

        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure(scope.Db.ConnectionString);
        using var provider = services.BuildServiceProvider();
        using var diScope = provider.CreateScope();

        var useCase = diScope.ServiceProvider.GetRequiredService<CalculerBulletin>();
        var demande = new CalculerBulletin.Demande(
            AgentId: "A-PILOTE", DatePaie: "2025-06-01",
            SourcesValeur: new Dictionary<string, decimal> { ["PAPP"] = 0.30m },
            ClesBareme: new Dictionary<string, string> { ["CATEGORIE"] = "13" },
            Profil: ProfilFiscal.Standard);

        var result = await useCase.ExecuterAsync(demande);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal(57739m, result.Value.Net);
    }

    [Fact]
    public void Tous_les_ports_et_use_cases_attendus_se_resolvent_sans_exception()
    {
        using var scope = SchemaTestSupport.CreateMigrated();

        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure(scope.Db.ConnectionString);
        using var provider = services.BuildServiceProvider();
        using var diScope = provider.CreateScope();
        var sp = diScope.ServiceProvider;

        Assert.NotNull(sp.GetRequiredService<IClock>());
        Assert.NotNull(sp.GetRequiredService<IAgentCarriereRepository>());
        Assert.NotNull(sp.GetRequiredService<IVariableRepository>());
        Assert.NotNull(sp.GetRequiredService<IPayrollReadRepository>());
        Assert.NotNull(sp.GetRequiredService<IAgentRepository>());
        Assert.NotNull(sp.GetRequiredService<IBulletinRepository>());
        Assert.NotNull(sp.GetRequiredService<IBulletinReadRepository>());
        Assert.NotNull(sp.GetRequiredService<IGrilleIndiciaireRepository>());

        Assert.NotNull(sp.GetRequiredService<CreerAgent>());
        Assert.NotNull(sp.GetRequiredService<CalculerBulletin>());
        Assert.NotNull(sp.GetRequiredService<ValiderBulletin>());
        Assert.NotNull(sp.GetRequiredService<ConsulterBulletin>());
        Assert.NotNull(sp.GetRequiredService<DefinirValeurPoint>());
        Assert.NotNull(sp.GetRequiredService<DefinirIndiceMinGrille>());
        Assert.NotNull(sp.GetRequiredService<DefinirIndiceEchelon>());
        Assert.NotNull(sp.GetRequiredService<SimulerEvolutionReglementaire>());
    }
}
