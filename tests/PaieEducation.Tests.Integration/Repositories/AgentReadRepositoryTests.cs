using Microsoft.Data.Sqlite;
using PaieEducation.Infrastructure.Repositories.Agents;

namespace PaieEducation.Tests.Integration.Repositories;

/// <summary>
/// Tests d'<see cref="AgentReadRepository"/> — <c>ListerAsync</c> (déjà en
/// production comme sélecteur de « Calculer un bulletin », jusqu'ici non testé
/// isolément) et <c>ObtenirAsync</c> (nouveau, chantier « Liste des agents +
/// fiche détail »).
/// </summary>
public class AgentReadRepositoryTests
{
    private static void SeedReferentiel(SqliteConnection c)
    {
        SchemaTestSupport.Exec(c, """
            INSERT INTO Filieres (Id, Libelle, CreatedAt, Hash) VALUES ('ENSEIGNANT', 'Enseignant', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ('PEM', 'Prof. École', 'ENSEIGNANT', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('PEM-G1', 'Professeur École primaire', 'PEM', 1, '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('13', 13, 'Catégorie 13', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('5', 5, 'Échelon 5', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Fonctions (Id, Libelle, CreatedAt, Hash) VALUES ('DIRECTEUR', 'Directeur', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Etablissements (Id, Nom, Type, CreatedAt, Hash) VALUES ('LYC001', 'Lycée test', 'LYCEE', '2026-01-01T00:00:00Z', 'h');
            """);
    }

    private static void SeedAgent(SqliteConnection c, string id, string matricule, string nom = "Test", string prenom = "Agent")
    {
        SchemaTestSupport.Exec(c, """
            INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, SituationFamiliale, CreatedAt)
            VALUES ($id, $matricule, $nom, $prenom, '1990-01-01', '2015-09-01', 'M', 'MARIE', '2026-01-01T00:00:00Z');
            """, ("$id", id), ("$matricule", matricule), ("$nom", nom), ("$prenom", prenom));
    }

    private static void SeedCarriere(
        SqliteConnection c, string id, string agentId, string dateEffet,
        string? fonctionId = null, string? etablissementId = null)
    {
        SchemaTestSupport.Exec(c, """
            INSERT INTO Carrieres
                (Id, AgentId, GradeId, CategorieId, EchelonId, FonctionId, TypeContrat, EtablissementId, DateEffet, Motif, CreatedAt)
            VALUES
                ($id, $agentId, 'PEM-G1', '13', '5', $fonctionId, 'STATUTAIRE', $etablissementId, $dateEffet, 'Recrutement', '2026-01-01T00:00:00Z');
            """, ("$id", id), ("$agentId", agentId), ("$fonctionId", fonctionId),
            ("$etablissementId", etablissementId), ("$dateEffet", dateEffet));
    }

    [Fact]
    public async Task ListerAsync_renvoie_tous_les_agents_tries_par_matricule()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedAgent(scope.Conn, "A-2", "MAT-002", "Kaci", "Fatima");
        SeedAgent(scope.Conn, "A-1", "MAT-001", "Benali", "Ahmed");

        var repo = new AgentReadRepository(scope.Conn);
        var result = await repo.ListerAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("MAT-001", result.Value[0].Matricule); // tri croissant
        Assert.Equal("MAT-002", result.Value[1].Matricule);
    }

    [Fact]
    public async Task ObtenirAsync_agent_avec_carriere_renvoie_l_identite_et_la_carriere()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        SeedAgent(scope.Conn, "A-1", "MAT-001");
        SeedCarriere(scope.Conn, "C-1", "A-1", "2015-09-01", fonctionId: "DIRECTEUR", etablissementId: "LYC001");

        var repo = new AgentReadRepository(scope.Conn);
        var detail = await repo.ObtenirAsync("A-1");

        Assert.NotNull(detail);
        Assert.Equal("MAT-001", detail!.Matricule);
        Assert.Equal("Test Agent", detail.NomComplet);
        Assert.Equal("ACTIF", detail.Statut);
        Assert.Equal("PEM-G1", detail.GradeId);
        Assert.Equal("Professeur École primaire", detail.GradeLibelle);
        Assert.Equal("Prof. École", detail.CorpsLibelle);
        Assert.Equal(13, detail.CategorieNiveau);
        Assert.Equal(5, detail.EchelonNumero);
        Assert.Equal("STATUTAIRE", detail.TypeContrat);
        Assert.Equal("Directeur", detail.FonctionLibelle);
        Assert.Equal("Lycée test", detail.EtablissementNom);
        Assert.Equal("LYCEE", detail.EtablissementType);
        Assert.Equal("2015-09-01", detail.CarriereDepuis);
        Assert.Equal("Recrutement", detail.CarriereMotif);
    }

    [Fact]
    public async Task ObtenirAsync_prend_la_carriere_la_plus_recente_quand_il_y_en_a_plusieurs()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        SeedAgent(scope.Conn, "A-1", "MAT-001");
        SeedCarriere(scope.Conn, "C-1", "A-1", "2015-09-01");
        SeedCarriere(scope.Conn, "C-2", "A-1", "2023-01-01"); // promotion, plus récente

        var repo = new AgentReadRepository(scope.Conn);
        var detail = await repo.ObtenirAsync("A-1");

        Assert.NotNull(detail);
        Assert.Equal("2023-01-01", detail!.CarriereDepuis);
    }

    [Fact]
    public async Task ObtenirAsync_agent_sans_carriere_renvoie_l_identite_seule()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedAgent(scope.Conn, "A-1", "MAT-001");
        // Aucune carrière insérée.

        var repo = new AgentReadRepository(scope.Conn);
        var detail = await repo.ObtenirAsync("A-1");

        Assert.NotNull(detail);
        Assert.Equal("MAT-001", detail!.Matricule);
        Assert.Null(detail.GradeId);
        Assert.Null(detail.CarriereDepuis);
    }

    [Fact]
    public async Task ObtenirAsync_agent_inexistant_renvoie_null()
    {
        using var scope = SchemaTestSupport.CreateMigrated();

        var repo = new AgentReadRepository(scope.Conn);
        var detail = await repo.ObtenirAsync("A-INEXISTANT");

        Assert.Null(detail);
    }
}
