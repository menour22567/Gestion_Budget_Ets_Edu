using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Agents;
using PaieEducation.Infrastructure.Repositories.Agents;

namespace PaieEducation.Tests.Integration.Repositories;

/// <summary>
/// Tests d'<see cref="AgentRepository"/> (Phase 5, tâche 4) : création d'un
/// agent et de sa carrière initiale (<c>Agents</c> + <c>Carrieres</c>, V011)
/// en une seule transaction.
/// </summary>
public class AgentRepositoryTests
{
    private static void SeedReferentiel(SqliteConnection c)
    {
        SchemaTestSupport.Exec(c, """
            INSERT INTO Filieres (Id, Libelle, CreatedAt, Hash) VALUES ('ENSEIGNANT', 'Enseignant', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ('PEM', 'Prof. École', 'ENSEIGNANT', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('PEM-G1', 'Grade 1', 'PEM', 1, '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('13', 13, 'Catégorie 13', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('5', 5, 'Échelon 5', '2026-01-01T00:00:00Z', 'h');
            """);
    }

    private static NouvelAgent Demande(string matricule = "MAT-001") => new(
        Matricule: matricule, Nom: "Test", Prenom: "Agent", DateNaissance: "1990-01-01",
        DateRecrutement: "2015-09-01", Sexe: "M", SituationFamiliale: "CELIBATAIRE",
        GradeId: "PEM-G1", CategorieId: "13", EchelonId: "5", TypeContrat: "STATUTAIRE");

    [Fact]
    public async Task CreerAsync_cree_l_agent_et_sa_carriere_initiale()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);

        var repo = new AgentRepository(scope.Conn);
        var result = await repo.CreerAsync(Demande(), new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        var agentId = result.Value;
        Assert.False(string.IsNullOrWhiteSpace(agentId));

        Assert.Equal("MAT-001", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT Matricule FROM Agents WHERE Id = @id;", ("@id", agentId)));
        Assert.Equal("2026-07-16T10:00:00.0000000Z", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT CreatedAt FROM Agents WHERE Id = @id;", ("@id", agentId)));

        Assert.Equal("PEM-G1", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT GradeId FROM Carrieres WHERE AgentId = @id;", ("@id", agentId)));
        Assert.Equal("2015-09-01", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateEffet FROM Carrieres WHERE AgentId = @id;", ("@id", agentId)));
        Assert.Equal("Recrutement", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT Motif FROM Carrieres WHERE AgentId = @id;", ("@id", agentId)));
    }

    [Fact]
    public async Task CreerAsync_matricule_deja_utilise_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);

        var repo = new AgentRepository(scope.Conn);
        var premier = await repo.CreerAsync(Demande(), DateTimeOffset.UtcNow);
        Assert.True(premier.IsSuccess);

        var doublon = await repo.CreerAsync(Demande(), DateTimeOffset.UtcNow);

        Assert.True(doublon.IsFailure);
        Assert.Contains("matricule", doublon.Error.Message, StringComparison.OrdinalIgnoreCase);

        var nbAgents = SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM Agents;");
        Assert.Equal(1, nbAgents);
    }
}
