using Microsoft.Data.Sqlite;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Infrastructure.Repositories.Agents;
using PaieEducation.Infrastructure.Repositories.Workbench;
using PaieEducation.Shared.Time;
using PaieEducation.Seeding;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Preuve d'intégration d'<see cref="AccepterSuggestion"/>,
/// <see cref="SupprimerAffectation"/> et <see cref="SuspendreAffectation"/>
/// (Phase 5, tâche 5, J3H §7) — enchaîne <see cref="SuggererRubriques"/>
/// (même cycle ISSRP réel que <see cref="SuggererRubriquesTests"/>) puis une
/// transition d'état sur la ligne produite.
/// </summary>
public class AffectationRubriqueUseCasesTests
{
    private sealed class HorlogeFixe(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
        public DateTimeOffset UtcNow => now;
        public DateOnly Today => DateOnly.FromDateTime(now.UtcDateTime);
    }

    private static readonly HorlogeFixe Horloge = new(new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero));

    private static async Task SeedTout(SqliteConnection conn)
    {
        await new ReglementaireSeeder().SeedAsync(conn);
        await new IrgSeeder().SeedAsync(conn);
        await new FormulesSeeder().SeedAsync(conn);
    }

    // Même agent conditionnel éligible que SuggererRubriquesTests.
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

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static AgentRubriqueRepository AgentRubriqueRepo(SqliteConnection conn) => new(conn);

    /// <summary>Seed + suggère, renvoie l'Id de la ligne AgentRubriques créée pour ISSRP_45.</summary>
    private static async Task<string> PreparerSuggestionAsync(SqliteConnection conn)
    {
        await SeedTout(conn);
        SeedAgentConditionnelEligible(conn);

        var suggerer = new SuggererRubriques(
            new AgentCarriereRepository(conn), new WorkbenchReadRepository(conn), AgentRubriqueRepo(conn), Horloge);
        var suggestion = await suggerer.ExecuterAsync(new SuggererRubriques.Demande("A-ELIGIBLE", "2025-06-01"));
        Assert.True(suggestion.IsSuccess, suggestion.IsFailure ? suggestion.Error.Message : null);
        Assert.Contains("ISSRP_45", suggestion.Value);

        return SchemaTestSupport.Scalar<string>(conn,
            "SELECT Id FROM AgentRubriques WHERE AgentId = 'A-ELIGIBLE' AND RubriqueId = 'ISSRP_45';");
    }

    [Fact]
    public async Task AccepterSuggestion_transitionne_SUGGEREE_vers_ACCEPTEE()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var agentRubriqueId = await PreparerSuggestionAsync(scope.Conn);

        var accepter = new AccepterSuggestion(AgentRubriqueRepo(scope.Conn), Horloge);
        var result = await accepter.ExecuterAsync(new AccepterSuggestion.Demande(agentRubriqueId));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("ACCEPTEE", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT Statut FROM AgentRubriques WHERE Id = @id;", ("@id", agentRubriqueId)));
    }

    [Fact]
    public async Task SuspendreAffectation_puis_AccepterSuggestion_reactive()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var agentRubriqueId = await PreparerSuggestionAsync(scope.Conn);
        var accepter = new AccepterSuggestion(AgentRubriqueRepo(scope.Conn), Horloge);
        await accepter.ExecuterAsync(new AccepterSuggestion.Demande(agentRubriqueId));

        var suspendre = new SuspendreAffectation(AgentRubriqueRepo(scope.Conn), Horloge);
        var suspension = await suspendre.ExecuterAsync(new SuspendreAffectation.Demande(agentRubriqueId));
        Assert.True(suspension.IsSuccess, suspension.IsFailure ? suspension.Error.Message : null);
        Assert.Equal("SUSPENDUE", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT Statut FROM AgentRubriques WHERE Id = @id;", ("@id", agentRubriqueId)));

        // Réactivation : SUSPENDUE -> ACCEPTEE via AccepterSuggestion (même cible, J3H §7).
        var reactivation = await accepter.ExecuterAsync(new AccepterSuggestion.Demande(agentRubriqueId));
        Assert.True(reactivation.IsSuccess);
        Assert.Equal("ACCEPTEE", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT Statut FROM AgentRubriques WHERE Id = @id;", ("@id", agentRubriqueId)));
    }

    [Fact]
    public async Task SupprimerAffectation_est_terminale_toute_nouvelle_transition_echoue()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var agentRubriqueId = await PreparerSuggestionAsync(scope.Conn);

        var supprimer = new SupprimerAffectation(AgentRubriqueRepo(scope.Conn), Horloge);
        var suppression = await supprimer.ExecuterAsync(new SupprimerAffectation.Demande(agentRubriqueId));
        Assert.True(suppression.IsSuccess, suppression.IsFailure ? suppression.Error.Message : null);
        Assert.Equal("SUPPRIMEE", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT Statut FROM AgentRubriques WHERE Id = @id;", ("@id", agentRubriqueId)));

        var accepter = new AccepterSuggestion(AgentRubriqueRepo(scope.Conn), Horloge);
        var tentative = await accepter.ExecuterAsync(new AccepterSuggestion.Demande(agentRubriqueId));

        Assert.True(tentative.IsFailure);
        Assert.Contains("terminal", tentative.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListerAffectationsAgent_reflete_l_etat_apres_transition()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var agentRubriqueId = await PreparerSuggestionAsync(scope.Conn);
        var accepter = new AccepterSuggestion(AgentRubriqueRepo(scope.Conn), Horloge);
        await accepter.ExecuterAsync(new AccepterSuggestion.Demande(agentRubriqueId));

        var lister = new ListerAffectationsAgent(AgentRubriqueRepo(scope.Conn));
        var result = await lister.ExecuterAsync(new ListerAffectationsAgent.Demande("A-ELIGIBLE", "2025-06-01"));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        var affectation = Assert.Single(result.Value, a => a.RubriqueId == "ISSRP_45");
        Assert.Equal(agentRubriqueId, affectation.Id);
        Assert.Equal("ACCEPTEE", affectation.Statut);
    }
}
