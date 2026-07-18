using Microsoft.Data.Sqlite;
using PaieEducation.Application.Agents.UseCases;
using PaieEducation.Domain.Agents;
using PaieEducation.Infrastructure.Repositories.Agents;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Preuve d'intégration de <see cref="CreerAgent"/> (Phase 5, tâche 4) : le
/// use case crée un agent via <see cref="AgentRepository"/>, et l'agent créé
/// est immédiatement résoluble par <see cref="AgentCarriereRepository"/>
/// (le port de lecture existant) — les deux use cases pilotes sont cohérents
/// sur le même schéma.
/// </summary>
public class CreerAgentTests
{
    private sealed class HorlogeFixe(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
        public DateTimeOffset UtcNow => now;
        public DateOnly Today => DateOnly.FromDateTime(now.UtcDateTime);
    }

    private static void SeedReferentiel(SqliteConnection c)
    {
        SchemaTestSupport.Exec(c, """
            INSERT INTO Filieres (Id, Libelle, CreatedAt, Hash) VALUES ('ENSEIGNANT', 'Enseignant', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ('PEM', 'Prof. École', 'ENSEIGNANT', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('PEM-G1', 'Grade 1', 'PEM', 1, '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('13', 13, 'Catégorie 13', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('5', 5, 'Échelon 5', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO TypesSexe (Id, Libelle, CreatedAt, Hash) VALUES ('M', 'Masculin', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO TypesSexe (Id, Libelle, CreatedAt, Hash) VALUES ('F', 'Féminin', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO SituationsFamiliales (Id, Libelle, CreatedAt, Hash) VALUES ('CELIBATAIRE', 'Célibataire', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO SituationsFamiliales (Id, Libelle, CreatedAt, Hash) VALUES ('MARIE', 'Marié(e)', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO TypesContrat (Id, Libelle, CreatedAt, Hash) VALUES ('STATUTAIRE', 'Statutaire', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO TypesContrat (Id, Libelle, CreatedAt, Hash) VALUES ('CONTRACTUEL', 'Contractuel', '2026-01-01T00:00:00Z', 'h');
            """);
    }

    private static NouvelAgent Demande(string sexe = "M", string situationFamiliale = "CELIBATAIRE", string typeContrat = "STATUTAIRE") => new(
        Matricule: "MAT-001", Nom: "Test", Prenom: "Agent", DateNaissance: "1990-01-01",
        DateRecrutement: "2015-09-01", Sexe: sexe, SituationFamiliale: situationFamiliale,
        GradeId: "PEM-G1", CategorieId: "13", EchelonId: "5", TypeContrat: typeContrat);

    [Fact]
    public async Task Executer_cree_un_agent_immediatement_resoluble_par_AgentCarriereRepository()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);

        var useCase = new CreerAgent(new AgentRepository(scope.Conn), new AgentReadRepository(scope.Conn), new HorlogeFixe(DateTimeOffset.UtcNow));
        var creation = await useCase.ExecuterAsync(Demande());
        Assert.True(creation.IsSuccess, creation.IsFailure ? creation.Error.Message : null);

        var contexte = await new AgentCarriereRepository(scope.Conn).ResoudreAsync(creation.Value, "2025-06-01");

        Assert.True(contexte.IsSuccess, contexte.IsFailure ? contexte.Error.Message : null);
        Assert.Equal("PEM-G1", contexte.Value.Grade);
        Assert.Equal(13, contexte.Value.Categorie);
        Assert.Equal(5, contexte.Value.Echelon);
    }

    [Fact]
    public async Task Executer_sexe_invalide_echoue_explicitement_sans_toucher_la_base()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);

        var useCase = new CreerAgent(new AgentRepository(scope.Conn), new AgentReadRepository(scope.Conn), new HorlogeFixe(DateTimeOffset.UtcNow));
        var result = await useCase.ExecuterAsync(Demande(sexe: "X"));

        Assert.True(result.IsFailure);
        Assert.Contains("Sexe", result.Error.Message);

        var nbAgents = SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM Agents;");
        Assert.Equal(0, nbAgents);
    }
}
