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

    // -------------------------------------------------------------------
    // ModifierAsync (chantier gestion des agents — identité)
    // -------------------------------------------------------------------

    [Fact]
    public async Task ModifierAsync_met_a_jour_l_identite_et_horodate()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        var repo = new AgentRepository(scope.Conn);
        var agentId = (await repo.CreerAsync(Demande(), DateTimeOffset.UtcNow)).Value;

        var demande = new AgentModifie(agentId, "Nouveau", "Prenom", "1991-02-02", "F", "MARIE", "SUSPENDU");
        var result = await repo.ModifierAsync(demande, new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("Nouveau", SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT Nom FROM Agents WHERE Id = @id;", ("@id", agentId)));
        Assert.Equal("SUSPENDU", SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT Statut FROM Agents WHERE Id = @id;", ("@id", agentId)));
        Assert.Equal("2026-07-21T10:00:00.0000000Z", SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT UpdatedAt FROM Agents WHERE Id = @id;", ("@id", agentId)));
        // Le matricule reste immuable (hors périmètre de ModifierAsync).
        Assert.Equal("MAT-001", SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT Matricule FROM Agents WHERE Id = @id;", ("@id", agentId)));
    }

    [Fact]
    public async Task ModifierAsync_agent_inexistant_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new AgentRepository(scope.Conn);

        var result = await repo.ModifierAsync(
            new AgentModifie("A-INEXISTANT", "N", "P", "1990-01-01", "M", "CELIBATAIRE", "ACTIF"), DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Contains("introuvable", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------
    // EnregistrerEvenementCarriereAsync (chantier gestion des agents)
    // -------------------------------------------------------------------

    [Fact]
    public async Task EnregistrerEvenementCarriereAsync_ferme_la_carriere_en_vigueur_et_insere_la_nouvelle()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('PEM-G2', 'Grade 2', 'PEM', 2, '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('14', 14, 'Catégorie 14', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('6', 6, 'Échelon 6', '2026-01-01T00:00:00Z', 'h');
            """);
        var repo = new AgentRepository(scope.Conn);
        var agentId = (await repo.CreerAsync(Demande(), new DateTimeOffset(2015, 9, 1, 0, 0, 0, TimeSpan.Zero))).Value;
        var ancienneCarriereId = SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT Id FROM Carrieres WHERE AgentId = @id;", ("@id", agentId));

        var demande = new EvenementCarriere(
            agentId, "PEM-G2", "14", "6", "STATUTAIRE", "2023-01-01", "Promotion de grade");
        var result = await repo.EnregistrerEvenementCarriereAsync(demande, DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        var nouvelleCarriereId = result.Value;

        Assert.Equal("2022-12-31", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM Carrieres WHERE Id = @id;", ("@id", ancienneCarriereId)));
        Assert.Equal("PEM-G2", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT GradeId FROM Carrieres WHERE Id = @id;", ("@id", nouvelleCarriereId)));
        Assert.Null(SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM Carrieres WHERE Id = @id;", ("@id", nouvelleCarriereId)));
        Assert.Equal(2L, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM Carrieres WHERE AgentId = @id;", ("@id", agentId)));
    }

    [Fact]
    public async Task EnregistrerEvenementCarriereAsync_date_effet_non_posterieure_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        var repo = new AgentRepository(scope.Conn);
        var agentId = (await repo.CreerAsync(Demande(), new DateTimeOffset(2015, 9, 1, 0, 0, 0, TimeSpan.Zero))).Value;

        // Antérieure (pas égale) à la carrière en vigueur — une date égale
        // déclencherait la garde de doublon exact (Conflict), pas celle-ci
        // (Validation), puisque existeDeja est vérifié en premier.
        var demande = new EvenementCarriere(agentId, "PEM-G1", "13", "5", "STATUTAIRE", "2010-01-01", "Test");
        var result = await repo.EnregistrerEvenementCarriereAsync(demande, DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
    }

    [Fact]
    public async Task EnregistrerEvenementCarriereAsync_agent_inexistant_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new AgentRepository(scope.Conn);

        var demande = new EvenementCarriere("A-INEXISTANT", "PEM-G1", "13", "5", "STATUTAIRE", "2025-01-01", "Test");
        var result = await repo.EnregistrerEvenementCarriereAsync(demande, DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Contains("introuvable", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------
    // DefinirAttributAsync (chantier gestion des agents)
    // -------------------------------------------------------------------

    [Fact]
    public async Task DefinirAttributAsync_insere_puis_ferme_la_version_en_vigueur_a_la_redefinition()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        var repo = new AgentRepository(scope.Conn);
        var agentId = (await repo.CreerAsync(Demande(), DateTimeOffset.UtcNow)).Value;

        var premiere = await repo.DefinirAttributAsync(agentId, "NOTATION_AGENT", "15", "2024-01-01", null, DateTimeOffset.UtcNow);
        Assert.True(premiere.IsSuccess, premiere.IsFailure ? premiere.Error.Message : null);

        var seconde = await repo.DefinirAttributAsync(agentId, "NOTATION_AGENT", "17", "2025-01-01", "Notation annuelle", DateTimeOffset.UtcNow);
        Assert.True(seconde.IsSuccess, seconde.IsFailure ? seconde.Error.Message : null);

        Assert.Equal("2024-12-31", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM AgentAttributs WHERE Id = @id;", ("@id", premiere.Value)));
        Assert.Equal("17", SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT Valeur FROM AgentAttributs WHERE Id = @id;", ("@id", seconde.Value)));
        Assert.Null(SchemaTestSupport.Scalar<string>(
            scope.Conn, "SELECT DateFin FROM AgentAttributs WHERE Id = @id;", ("@id", seconde.Value)));
    }

    [Fact]
    public async Task DefinirAttributAsync_meme_date_effet_echoue_en_conflit()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        var repo = new AgentRepository(scope.Conn);
        var agentId = (await repo.CreerAsync(Demande(), DateTimeOffset.UtcNow)).Value;
        await repo.DefinirAttributAsync(agentId, "ORIGINE_STATUTAIRE", "ENSEIGNANT", "2024-01-01", null, DateTimeOffset.UtcNow);

        var result = await repo.DefinirAttributAsync(agentId, "ORIGINE_STATUTAIRE", "AUTRE", "2024-01-01", null, DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
    }

    [Fact]
    public async Task DefinirAttributAsync_agent_inexistant_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var repo = new AgentRepository(scope.Conn);

        var result = await repo.DefinirAttributAsync("A-INEXISTANT", "NOTATION_AGENT", "15", "2024-01-01", null, DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Contains("introuvable", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
