using Microsoft.Data.Sqlite;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Infrastructure.Repositories.Agents;
using PaieEducation.Infrastructure.Repositories.Workbench;
using PaieEducation.Shared.Time;
using PaieEducation.Tools.Seeding;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Preuve d'intégration de <see cref="SuggererRubriques"/> (Phase 5,
/// tâche 5, D5) : rejoue le cycle ISSRP documenté en J3H §10(a), avec les
/// mêmes groupes DNF réels (<c>GE-ISSRP45-DIRECT</c>/<c>GE-ISSRP45-ORIGINE</c>,
/// J4F) que <see cref="Calcul.BulletinEndToEndTests"/>.
/// </summary>
public class SuggererRubriquesTests
{
    private sealed class HorlogeFixe(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
        public DateTimeOffset UtcNow => now;
        public DateOnly Today => DateOnly.FromDateTime(now.UtcDateTime);
    }

    private static readonly HorlogeFixe Horloge = new(new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero));

    private static async Task SeedTout(SqliteConnection conn)
    {
        await new ReglementaireSeeder().SeedAsync(conn);
        await new IrgSeeder().SeedAsync(conn);
        await new FormulesSeeder().SeedAsync(conn);
    }

    // SDL-G007 = "Educateur specialise en soutien Educatif" — grade
    // conditionnel (Q-C1) : satisfait GE-ISSRP45-ORIGINE si
    // ORIGINE_STATUTAIRE = ENSEIGNANT (même grade que
    // BulletinEndToEndTests.Enseignant_grade_conditionnel_origine_ENSEIGNANT_a_45_pourcent).
    private static void SeedAgentConditionnelEligible(SqliteConnection c) => Exec(c, """
        INSERT INTO Filieres (Id, Libelle, CreatedAt, Hash) VALUES ('SOUTIEN', 'Soutien éducatif', '2026-01-01T00:00:00Z', 'h');
        INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ('SDL', 'Soutien', 'SOUTIEN', '2026-01-01T00:00:00Z', 'h');
        INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('SDL-G007', 'Educateur specialise en soutien Educatif', 'SDL', 1, '2026-01-01T00:00:00Z', 'h');
        INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('10', 10, 'Catégorie 10', '2026-01-01T00:00:00Z', 'h');
        INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('3', 3, 'Échelon 3', '2026-01-01T00:00:00Z', 'h');
        INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, CreatedAt)
            VALUES ('A-ELIGIBLE', 'MAT-ELIGIBLE', 'Test', 'Eligible', '1985-01-01', '2010-09-01', 'M', '2026-01-01T00:00:00Z');
        INSERT INTO Carrieres (Id, AgentId, GradeId, CategorieId, EchelonId, TypeContrat, DateEffet, Motif, CreatedAt)
            VALUES ('C-ELIGIBLE', 'A-ELIGIBLE', 'SDL-G007', '10', '3', 'STATUTAIRE', '2010-09-01', 'Recrutement', '2026-01-01T00:00:00Z');
        INSERT INTO AgentAttributs (Id, AgentId, Attribut, Valeur, DateEffet, CreatedAt)
            VALUES ('AA-ELIGIBLE', 'A-ELIGIBLE', 'ORIGINE_STATUTAIRE', 'ENSEIGNANT', '2010-09-01', '2026-01-01T00:00:00Z');
        """);

    // A-G048 = "Administrateur" — hors groupe ISSRP (corps hors EN, J4F),
    // même grade que BulletinEndToEndTests.Enseignant_hors_groupe_ISSRP_n_a_pas_la_prime.
    private static void SeedAgentHorsGroupe(SqliteConnection c) => Exec(c, """
        INSERT INTO Filieres (Id, Libelle, CreatedAt, Hash) VALUES ('ADMIN', 'Administration', '2026-01-01T00:00:00Z', 'h');
        INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ('A', 'Administrateurs', 'ADMIN', '2026-01-01T00:00:00Z', 'h');
        INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('A-G048', 'Administrateur', 'A', 1, '2026-01-01T00:00:00Z', 'h');
        INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('12', 12, 'Catégorie 12', '2026-01-01T00:00:00Z', 'h');
        INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('4', 4, 'Échelon 4', '2026-01-01T00:00:00Z', 'h');
        INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, CreatedAt)
            VALUES ('A-HORS-GROUPE', 'MAT-HORS', 'Test', 'HorsGroupe', '1985-01-01', '2010-09-01', 'M', '2026-01-01T00:00:00Z');
        INSERT INTO Carrieres (Id, AgentId, GradeId, CategorieId, EchelonId, TypeContrat, DateEffet, Motif, CreatedAt)
            VALUES ('C-HORS-GROUPE', 'A-HORS-GROUPE', 'A-G048', '12', '4', 'STATUTAIRE', '2010-09-01', 'Recrutement', '2026-01-01T00:00:00Z');
        """);

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static SuggererRubriques BuildUseCase(SqliteConnection conn) => new(
        new AgentCarriereRepository(conn), new WorkbenchReadRepository(conn),
        new AgentRubriqueRepository(conn), Horloge);

    [Fact]
    public async Task Agent_conditionnel_eligible_recoit_la_suggestion_ISSRP_45_avec_l_origine_ORIGINE()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentConditionnelEligible(scope.Conn);

        var useCase = BuildUseCase(scope.Conn);
        var result = await useCase.ExecuterAsync(new SuggererRubriques.Demande("A-ELIGIBLE", "2025-06-01"));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Contains("ISSRP_45", result.Value);

        var origine = SchemaTestSupport.Scalar<string>(scope.Conn,
            "SELECT Origine FROM AgentRubriques WHERE AgentId = @a AND RubriqueId = 'ISSRP_45';", ("@a", "A-ELIGIBLE"));
        Assert.StartsWith("GROUPE:GE-ISSRP45-ORIGINE@", origine);
    }

    [Fact]
    public async Task Agent_hors_groupe_ne_recoit_pas_la_suggestion_ISSRP_45()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentHorsGroupe(scope.Conn);

        var useCase = BuildUseCase(scope.Conn);
        var result = await useCase.ExecuterAsync(new SuggererRubriques.Demande("A-HORS-GROUPE", "2025-06-01"));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.DoesNotContain("ISSRP_45", result.Value);
    }

    [Fact]
    public async Task Deuxieme_execution_est_idempotente()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentConditionnelEligible(scope.Conn);

        var useCase = BuildUseCase(scope.Conn);
        var premier = await useCase.ExecuterAsync(new SuggererRubriques.Demande("A-ELIGIBLE", "2025-06-01"));
        Assert.Contains("ISSRP_45", premier.Value);

        var second = await useCase.ExecuterAsync(new SuggererRubriques.Demande("A-ELIGIBLE", "2025-06-01"));

        Assert.True(second.IsSuccess);
        Assert.DoesNotContain("ISSRP_45", second.Value); // déjà suggérée — pas de nouvelle ligne.
        Assert.Equal(1, SchemaTestSupport.Scalar<long>(
            scope.Conn, "SELECT COUNT(*) FROM AgentRubriques WHERE AgentId = 'A-ELIGIBLE' AND RubriqueId = 'ISSRP_45';"));
    }
}
