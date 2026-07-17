using Microsoft.Data.Sqlite;
using PaieEducation.Infrastructure.Repositories.Workbench;

namespace PaieEducation.Tests.Integration.Repositories;

/// <summary>
/// Tests d'<see cref="AgentRubriqueRepository"/> (Phase 5, tâche 5, D5,
/// J3H §7) : création idempotente d'affectations suggérées.
/// </summary>
public class AgentRubriqueRepositoryTests
{
    private static void SeedAgentEtRubrique(SqliteConnection c) => SchemaTestSupport.Exec(c, """
        INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, CreatedAt)
        VALUES ('A-1', 'MAT-001', 'Test', 'Agent', '1990-01-01', '2015-09-01', 'M', '2026-01-01T00:00:00Z');
        INSERT INTO Rubriques (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul, CreatedAt, Hash)
        VALUES ('ISSRP_45', 'Soutien scolaire 45%', 'GAIN', 'TRAITEMENT', 'MENSUELLE', 230, '2026-01-01T00:00:00Z', 'h');
        """);

    [Fact]
    public async Task SuggererAsync_cree_une_ligne_SUGGEREE()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedAgentEtRubrique(scope.Conn);
        var repo = new AgentRubriqueRepository(scope.Conn);

        var result = await repo.SuggererAsync(
            "A-1", "ISSRP_45", 1, "GROUPE:GE-ISSRP45-ORIGINE@2025-01-01", "2025-06-01", DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.NotNull(result.Value);
        Assert.Equal("SUGGEREE", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT Statut FROM AgentRubriques WHERE Id = @id;", ("@id", result.Value!)));
        Assert.Equal("GROUPE:GE-ISSRP45-ORIGINE@2025-01-01", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT Origine FROM AgentRubriques WHERE Id = @id;", ("@id", result.Value!)));
    }

    [Fact]
    public async Task SuggererAsync_deuxieme_appel_identique_ne_duplique_pas()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedAgentEtRubrique(scope.Conn);
        var repo = new AgentRubriqueRepository(scope.Conn);

        var premier = await repo.SuggererAsync(
            "A-1", "ISSRP_45", 1, "GROUPE:GE-ISSRP45-ORIGINE@2025-01-01", "2025-06-01", DateTimeOffset.UtcNow);
        Assert.NotNull(premier.Value);

        var second = await repo.SuggererAsync(
            "A-1", "ISSRP_45", 1, "GROUPE:GE-ISSRP45-ORIGINE@2025-01-01", "2025-06-01", DateTimeOffset.UtcNow);

        Assert.True(second.IsSuccess);
        Assert.Null(second.Value);
        Assert.Equal(1, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM AgentRubriques;"));
    }

    [Fact]
    public async Task SuggererAsync_ligne_acceptee_existante_bloque_aussi_une_nouvelle_suggestion()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedAgentEtRubrique(scope.Conn);
        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO AgentRubriques (Id, AgentId, RubriqueId, Occurrence, Statut, Origine, DateEffet, CreatedAt)
            VALUES ('AR-1', 'A-1', 'ISSRP_45', 1, 'ACCEPTEE', 'MANUELLE', '2025-01-01', '2026-01-01T00:00:00Z');
            """);
        var repo = new AgentRubriqueRepository(scope.Conn);

        var result = await repo.SuggererAsync(
            "A-1", "ISSRP_45", 1, "GROUPE:GE-ISSRP45-ORIGINE@2025-01-01", "2025-06-01", DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(1, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM AgentRubriques;"));
    }

    [Fact]
    public async Task ListerParAgentAsync_renvoie_les_lignes_couvrant_la_date_y_compris_SUPPRIMEE()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedAgentEtRubrique(scope.Conn);
        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO AgentRubriques (Id, AgentId, RubriqueId, Occurrence, Statut, Origine, DateEffet, CreatedAt)
            VALUES ('AR-1', 'A-1', 'ISSRP_45', 1, 'SUPPRIMEE', 'MANUELLE', '2025-01-01', '2026-01-01T00:00:00Z');
            """);
        var repo = new AgentRubriqueRepository(scope.Conn);

        var result = await repo.ListerParAgentAsync("A-1", "2025-06-01");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        var affectation = Assert.Single(result.Value);
        Assert.Equal("AR-1", affectation.Id);
        Assert.Equal("SUPPRIMEE", affectation.Statut);
    }

    [Fact]
    public async Task ChangerStatutAsync_transition_nominale()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedAgentEtRubrique(scope.Conn);
        var repo = new AgentRubriqueRepository(scope.Conn);
        var suggestion = await repo.SuggererAsync(
            "A-1", "ISSRP_45", 1, "GROUPE:GE-ISSRP45-ORIGINE@2025-01-01", "2025-06-01", DateTimeOffset.UtcNow);

        var result = await repo.ChangerStatutAsync(suggestion.Value!, "ACCEPTEE", DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("ACCEPTEE", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT Statut FROM AgentRubriques WHERE Id = @id;", ("@id", suggestion.Value!)));
    }

    [Fact]
    public async Task ChangerStatutAsync_ligne_SUPPRIMEE_est_terminale()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedAgentEtRubrique(scope.Conn);
        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO AgentRubriques (Id, AgentId, RubriqueId, Occurrence, Statut, Origine, DateEffet, CreatedAt)
            VALUES ('AR-1', 'A-1', 'ISSRP_45', 1, 'SUPPRIMEE', 'MANUELLE', '2025-01-01', '2026-01-01T00:00:00Z');
            """);
        var repo = new AgentRubriqueRepository(scope.Conn);

        var result = await repo.ChangerStatutAsync("AR-1", "ACCEPTEE", DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Contains("terminal", result.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("SUPPRIMEE", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT Statut FROM AgentRubriques WHERE Id = 'AR-1';"));
    }

    [Fact]
    public async Task ChangerStatutAsync_ligne_inexistante_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new AgentRubriqueRepository(scope.Conn);

        var result = await repo.ChangerStatutAsync("AR-INEXISTANT", "ACCEPTEE", DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Contains("introuvable", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
