using Microsoft.Data.Sqlite;
using PaieEducation.Application.Agents.UseCases;
using PaieEducation.Infrastructure.Repositories.Agents;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Preuve d'intégration de <see cref="ConsulterFicheAgent"/> (chantier « Liste
/// des agents + fiche détail ») — même patron que
/// <see cref="ConsulterFicheRubriqueTests"/> : le use case est un mince
/// traducteur du « non trouvé » du port de lecture en erreur métier.
/// </summary>
public class ConsulterFicheAgentTests
{
    private static void SeedAgentEtCarriere(SqliteConnection c)
    {
        SchemaTestSupport.Exec(c, """
            INSERT INTO Filieres (Id, Libelle, CreatedAt, Hash) VALUES ('ENSEIGNANT', 'Enseignant', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ('PEM', 'Prof. École', 'ENSEIGNANT', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('PEM-G1', 'Professeur École primaire', 'PEM', 1, '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('13', 13, 'Catégorie 13', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('5', 5, 'Échelon 5', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, SituationFamiliale, CreatedAt)
            VALUES ('A-1', 'MAT-001', 'Benali', 'Ahmed', '1990-01-01', '2015-09-01', 'M', 'MARIE', '2026-01-01T00:00:00Z');
            INSERT INTO Carrieres (Id, AgentId, GradeId, CategorieId, EchelonId, TypeContrat, DateEffet, Motif, CreatedAt)
            VALUES ('C-1', 'A-1', 'PEM-G1', '13', '5', 'STATUTAIRE', '2015-09-01', 'Recrutement', '2026-01-01T00:00:00Z');
            """);
    }

    [Fact]
    public async Task Executer_agrege_identite_et_carriere_pour_un_agent_existant()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedAgentEtCarriere(scope.Conn);
        var useCase = new ConsulterFicheAgent(new AgentReadRepository(scope.Conn));

        var result = await useCase.ExecuterAsync(new ConsulterFicheAgent.Demande("A-1"));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("MAT-001", result.Value.Matricule);
        Assert.Equal("Benali Ahmed", result.Value.NomComplet);
        Assert.Equal("PEM-G1", result.Value.GradeId);
    }

    [Fact]
    public async Task Executer_agent_inexistant_echoue_avec_not_found()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var useCase = new ConsulterFicheAgent(new AgentReadRepository(scope.Conn));

        var result = await useCase.ExecuterAsync(new ConsulterFicheAgent.Demande("A-INEXISTANT"));

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }
}
