using Microsoft.Data.Sqlite;
using PaieEducation.Application.Agents.UseCases;
using PaieEducation.Domain.Agents;
using PaieEducation.Infrastructure.Repositories.Agents;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Preuve d'intégration de <see cref="ModifierAgent"/> (chantier gestion des
/// agents) — même patron que <see cref="CreerAgentTests"/> : validation des
/// valeurs énumérées avant écriture réelle.
/// </summary>
public class ModifierAgentTests
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
            """);
    }

    private static NouvelAgent DemandeCreation() => new(
        Matricule: "MAT-001", Nom: "Test", Prenom: "Agent", DateNaissance: "1990-01-01",
        DateRecrutement: "2015-09-01", Sexe: "M", SituationFamiliale: "CELIBATAIRE",
        GradeId: "PEM-G1", CategorieId: "13", EchelonId: "5", TypeContrat: "STATUTAIRE");

    private static async Task<string> CreerAgentAsync(SqliteConnection c)
    {
        var result = await new AgentRepository(c).CreerAsync(DemandeCreation(), DateTimeOffset.UtcNow);
        return result.Value;
    }

    [Fact]
    public async Task Executer_modifie_l_identite_avec_succes()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        var agentId = await CreerAgentAsync(scope.Conn);

        var useCase = new ModifierAgent(
            new AgentRepository(scope.Conn), new AgentReadRepository(scope.Conn), new HorlogeFixe(DateTimeOffset.UtcNow));
        var demande = new AgentModifie(agentId, "Nouveau", "Prenom", "1991-02-02", "F", "MARIE", "SUSPENDU");
        var result = await useCase.ExecuterAsync(demande);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("Nouveau", SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT Nom FROM Agents WHERE Id = @id;", ("@id", agentId)));
    }

    [Fact]
    public async Task Executer_sexe_invalide_echoue_explicitement_sans_toucher_la_base()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        var agentId = await CreerAgentAsync(scope.Conn);

        var useCase = new ModifierAgent(
            new AgentRepository(scope.Conn), new AgentReadRepository(scope.Conn), new HorlogeFixe(DateTimeOffset.UtcNow));
        var demande = new AgentModifie(agentId, "Nouveau", "Prenom", "1991-02-02", "X", "MARIE", "ACTIF");
        var result = await useCase.ExecuterAsync(demande);

        Assert.True(result.IsFailure);
        Assert.Contains("Sexe", result.Error.Message);
        Assert.Equal("Test", SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT Nom FROM Agents WHERE Id = @id;", ("@id", agentId)));
    }

    [Fact]
    public async Task Executer_statut_invalide_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        var agentId = await CreerAgentAsync(scope.Conn);

        var useCase = new ModifierAgent(
            new AgentRepository(scope.Conn), new AgentReadRepository(scope.Conn), new HorlogeFixe(DateTimeOffset.UtcNow));
        var demande = new AgentModifie(agentId, "Nouveau", "Prenom", "1991-02-02", "M", "CELIBATAIRE", "INVALIDE");
        var result = await useCase.ExecuterAsync(demande);

        Assert.True(result.IsFailure);
        Assert.Contains("Statut", result.Error.Message);
    }
}
