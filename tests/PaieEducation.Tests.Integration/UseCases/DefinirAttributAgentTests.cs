using Microsoft.Data.Sqlite;
using PaieEducation.Application.Agents.UseCases;
using PaieEducation.Domain.Agents;
using PaieEducation.Infrastructure.Repositories.Agents;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Preuve d'intégration de <see cref="DefinirAttributAgent"/> (chantier
/// gestion des agents) — l'attribut défini est immédiatement résolu par
/// <see cref="AgentCarriereRepository"/> (le même port que consomme le
/// moteur de calcul pour NOTATION_AGENT/ORIGINE_STATUTAIRE), fermant le trou
/// identifié par l'audit du 21/07/2026 (aucun chemin d'écriture applicatif).
/// </summary>
public class DefinirAttributAgentTests
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
            INSERT INTO SituationsFamiliales (Id, Libelle, CreatedAt, Hash) VALUES ('CELIBATAIRE', 'Célibataire', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO TypesContrat (Id, Libelle, CreatedAt, Hash) VALUES ('STATUTAIRE', 'Statutaire', '2026-01-01T00:00:00Z', 'h');
            """);
    }

    private static NouvelAgent DemandeCreation() => new(
        Matricule: "MAT-001", Nom: "Test", Prenom: "Agent", DateNaissance: "1990-01-01",
        DateRecrutement: "2015-09-01", Sexe: "M", SituationFamiliale: "CELIBATAIRE",
        GradeId: "PEM-G1", CategorieId: "13", EchelonId: "5", TypeContrat: "STATUTAIRE");

    private static async Task<string> CreerAgentAsync(SqliteConnection c)
        => (await new AgentRepository(c).CreerAsync(DemandeCreation(), new DateTimeOffset(2015, 9, 1, 0, 0, 0, TimeSpan.Zero))).Value;

    [Fact]
    public async Task Executer_definit_la_notation_et_elle_devient_resoluble_par_le_moteur()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        var agentId = await CreerAgentAsync(scope.Conn);

        var useCase = new DefinirAttributAgent(new AgentRepository(scope.Conn), new HorlogeFixe(DateTimeOffset.UtcNow));
        var demande = new DefinirAttributAgent.Demande(agentId, "NOTATION_AGENT", "16", "2024-01-01", "Notation 2024");
        var result = await useCase.ExecuterAsync(demande);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);

        var contexte = await new AgentCarriereRepository(scope.Conn).ResoudreAsync(agentId, "2025-01-01");
        Assert.True(contexte.IsSuccess);
        Assert.Equal(16m, contexte.Value.Note);
    }

    [Fact]
    public async Task Executer_redefinition_a_une_date_ulterieure_ferme_la_precedente()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        var agentId = await CreerAgentAsync(scope.Conn);
        var useCase = new DefinirAttributAgent(new AgentRepository(scope.Conn), new HorlogeFixe(DateTimeOffset.UtcNow));

        await useCase.ExecuterAsync(new DefinirAttributAgent.Demande(agentId, "ORIGINE_STATUTAIRE", "ENSEIGNANT", "2020-01-01"));
        var second = await useCase.ExecuterAsync(new DefinirAttributAgent.Demande(agentId, "ORIGINE_STATUTAIRE", "AUTRE", "2024-01-01"));
        Assert.True(second.IsSuccess, second.IsFailure ? second.Error.Message : null);

        var avant = await new AgentCarriereRepository(scope.Conn).ResoudreAsync(agentId, "2022-01-01");
        Assert.True(avant.IsSuccess, avant.IsFailure ? avant.Error.Message : null);
        Assert.Equal("ENSEIGNANT", avant.Value.OrigineStatutaire);

        var apres = await new AgentCarriereRepository(scope.Conn).ResoudreAsync(agentId, "2025-01-01");
        Assert.True(apres.IsSuccess, apres.IsFailure ? apres.Error.Message : null);
        Assert.Equal("AUTRE", apres.Value.OrigineStatutaire);
    }
}
