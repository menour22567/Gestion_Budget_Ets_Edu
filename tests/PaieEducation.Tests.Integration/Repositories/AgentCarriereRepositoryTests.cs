using Microsoft.Data.Sqlite;
using PaieEducation.Infrastructure.Repositories.Agents;

namespace PaieEducation.Tests.Integration.Repositories;

/// <summary>
/// Tests d'<see cref="AgentCarriereRepository"/> (Phase 5, jalon D) : résolution
/// point-in-time d'un <c>AgentContext</c> depuis Agents/Carrieres/AgentAttributs.
/// </summary>
public class AgentCarriereRepositoryTests
{
    private static void SeedReferentiel(SqliteConnection c)
    {
        SchemaTestSupport.Exec(c, """
            INSERT INTO Filieres (Id, Libelle, CreatedAt, Hash) VALUES ('ENSEIGNANT', 'Enseignant', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ('PEM', 'Prof. École', 'ENSEIGNANT', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('PEM-G1', 'Grade 1', 'PEM', 1, '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('PEM-G2', 'Grade 2', 'PEM', 2, '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('13', 13, 'Catégorie 13', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('14', 14, 'Catégorie 14', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('5', 5, 'Échelon 5', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('6', 6, 'Échelon 6', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Fonctions (Id, Libelle, CreatedAt, Hash) VALUES ('DIRECTEUR', 'Directeur', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Etablissements (Id, Nom, Type, CreatedAt, Hash) VALUES ('LYC001', 'Lycée test', 'LYCEE', '2026-01-01T00:00:00Z', 'h');
            """);
    }

    private static void SeedAgent(SqliteConnection c, string id, string matricule, string dateRecrutement)
    {
        SchemaTestSupport.Exec(c, """
            INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, CreatedAt)
            VALUES ($id, $matricule, 'Test', 'Agent', '1990-01-01', $dr, 'M', '2026-01-01T00:00:00Z');
            """, ("$id", id), ("$matricule", matricule), ("$dr", dateRecrutement));
    }

    private static void SeedCarriere(
        SqliteConnection c, string id, string agentId, string gradeId, string categorieId,
        string echelonId, string dateEffet, string? fonctionId = null, string? etablissementId = null,
        string typeContrat = "STATUTAIRE")
    {
        SchemaTestSupport.Exec(c, """
            INSERT INTO Carrieres
                (Id, AgentId, GradeId, CategorieId, EchelonId, FonctionId, TypeContrat, EtablissementId, DateEffet, Motif, CreatedAt)
            VALUES
                ($id, $agentId, $gradeId, $categorieId, $echelonId, $fonctionId, $typeContrat, $etablissementId, $dateEffet, 'Test', '2026-01-01T00:00:00Z');
            """, ("$id", id), ("$agentId", agentId), ("$gradeId", gradeId), ("$categorieId", categorieId),
            ("$echelonId", echelonId), ("$fonctionId", fonctionId), ("$typeContrat", typeContrat),
            ("$etablissementId", etablissementId), ("$dateEffet", dateEffet));
    }

    private static void SeedAttribut(SqliteConnection c, string id, string agentId, string attribut, string valeur, string dateEffet)
    {
        SchemaTestSupport.Exec(c, """
            INSERT INTO AgentAttributs (Id, AgentId, Attribut, Valeur, DateEffet, CreatedAt)
            VALUES ($id, $agentId, $attribut, $valeur, $dateEffet, '2026-01-01T00:00:00Z');
            """, ("$id", id), ("$agentId", agentId), ("$attribut", attribut), ("$valeur", valeur), ("$dateEffet", dateEffet));
    }

    [Fact]
    public async Task Resolution_nominale_renvoie_un_contexte_complet()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        SeedAgent(scope.Conn, "A-1", "MAT-001", "2015-09-01");
        SeedCarriere(scope.Conn, "C-1", "A-1", "PEM-G1", "13", "5", "2015-09-01",
            fonctionId: "DIRECTEUR", etablissementId: "LYC001");
        SeedAttribut(scope.Conn, "AA-1", "A-1", "ORIGINE_STATUTAIRE", "ENSEIGNANT", "2015-09-01");

        var repo = new AgentCarriereRepository(scope.Conn);
        var result = await repo.ResoudreAsync("A-1", "2025-06-01");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        var ctx = result.Value;
        Assert.Equal("ENSEIGNANT", ctx.Filiere);
        Assert.Equal("PEM", ctx.Corps);
        Assert.Equal("PEM-G1", ctx.Grade);
        Assert.Equal(13, ctx.Categorie);
        Assert.Equal(5, ctx.Echelon);
        Assert.Equal("DIRECTEUR", ctx.Fonction);
        Assert.Equal("STATUTAIRE", ctx.TypeContrat);
        Assert.Equal("LYCEE", ctx.TypeEtablissement);
        Assert.Equal("ENSEIGNANT", ctx.OrigineStatutaire);
        Assert.Equal(9, ctx.AncienneteAnnees); // 2015-09-01 -> 2025-06-01 = 9 ans révolus
        Assert.Null(ctx.Note);
        Assert.Null(ctx.ValeurPointIndiciaire);
    }

    [Fact]
    public async Task Origine_statutaire_absente_resout_en_INCONNU_jamais_null()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        SeedAgent(scope.Conn, "A-1", "MAT-001", "2015-09-01");
        SeedCarriere(scope.Conn, "C-1", "A-1", "PEM-G1", "13", "5", "2015-09-01");
        // Pas d'AgentAttributs — abstention réglementaire (ADR-0009, Q-C1).

        var repo = new AgentCarriereRepository(scope.Conn);
        var result = await repo.ResoudreAsync("A-1", "2025-06-01");

        Assert.True(result.IsSuccess);
        Assert.Equal("INCONNU", result.Value.OrigineStatutaire);
    }

    [Fact]
    public async Task Changement_de_grade_en_cours_de_carriere_resout_le_bon_grade_selon_la_date()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        SeedAgent(scope.Conn, "A-1", "MAT-001", "2015-09-01");
        SeedCarriere(scope.Conn, "C-1", "A-1", "PEM-G1", "13", "5", "2015-09-01");
        SeedCarriere(scope.Conn, "C-2", "A-1", "PEM-G2", "14", "6", "2023-01-01"); // promotion

        var repo = new AgentCarriereRepository(scope.Conn);

        var avant = await repo.ResoudreAsync("A-1", "2022-06-01");
        Assert.True(avant.IsSuccess);
        Assert.Equal("PEM-G1", avant.Value.Grade);
        Assert.Equal(13, avant.Value.Categorie);

        var apres = await repo.ResoudreAsync("A-1", "2023-06-01");
        Assert.True(apres.IsSuccess);
        Assert.Equal("PEM-G2", apres.Value.Grade);
        Assert.Equal(14, apres.Value.Categorie);
    }

    [Fact]
    public async Task Date_anterieure_a_toute_carriere_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        SeedAgent(scope.Conn, "A-1", "MAT-001", "2015-09-01");
        SeedCarriere(scope.Conn, "C-1", "A-1", "PEM-G1", "13", "5", "2015-09-01");

        var repo = new AgentCarriereRepository(scope.Conn);
        var result = await repo.ResoudreAsync("A-1", "2010-01-01");

        Assert.True(result.IsFailure);
        Assert.Contains("carrière", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Agent_inexistant_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();

        var repo = new AgentCarriereRepository(scope.Conn);
        var result = await repo.ResoudreAsync("A-INEXISTANT", "2025-06-01");

        Assert.True(result.IsFailure);
        Assert.Contains("introuvable", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------
    // Lot 1.2 — INDICE_ECHELON et ANCIENNETE_PRIVEE
    // ---------------------------------------------------------------------

    private static void SeedIndiceEchelon(
        SqliteConnection c, string id, string echelonId, string dateEffet, int indice)
    {
        SchemaTestSupport.Exec(c, """
            INSERT INTO IndicesEchelon (Id, EchelonId, DateEffet, Indice, Version, Hash, CreatedAt)
            VALUES ($id, $echelonId, $dateEffet, $indice, 'v1', 'h', '2026-01-01T00:00:00Z');
            """, ("$id", id), ("$echelonId", echelonId), ("$dateEffet", dateEffet), ("$indice", indice));
    }

    [Fact]
    public async Task IndiceEchelon_est_lu_depuis_la_grille_pour_le_numero_de_lechelon()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        SeedAgent(scope.Conn, "A-1", "MAT-001", "2015-09-01");
        SeedCarriere(scope.Conn, "C-1", "A-1", "PEM-G1", "13", "5", "2015-09-01");
        SeedIndiceEchelon(scope.Conn, "IE-5-2020", "5", "2020-01-01", 100);

        var repo = new AgentCarriereRepository(scope.Conn);
        var result = await repo.ResoudreAsync("A-1", "2025-06-01");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        // Indice 100 (≠ n° d'échelon 5 — la confusion entre les deux est un
        // risque identifié Lot 1.2).
        Assert.Equal(100m, result.Value.IndiceEchelon);
        Assert.Equal(5, result.Value.Echelon); // n° d'échelon inchangé
    }

    [Fact]
    public async Task IndiceEchelon_est_null_quand_aucune_grille_ne_couvre_la_date()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        SeedAgent(scope.Conn, "A-1", "MAT-001", "2015-09-01");
        SeedCarriere(scope.Conn, "C-1", "A-1", "PEM-G1", "13", "5", "2015-09-01");
        SeedIndiceEchelon(scope.Conn, "IE-5-2020", "5", "2030-01-01", 200); // futur

        var repo = new AgentCarriereRepository(scope.Conn);
        var result = await repo.ResoudreAsync("A-1", "2025-06-01");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.IndiceEchelon);
    }

    [Fact]
    public async Task AnciennetePrivee_est_lue_depuis_AgentAttributs_versionne()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        SeedAgent(scope.Conn, "A-1", "MAT-001", "2015-09-01");
        SeedCarriere(scope.Conn, "C-1", "A-1", "PEM-G1", "13", "5", "2015-09-01");
        SeedAttribut(scope.Conn, "AA-PRIV", "A-1", "ANCIENNETE_PRIVEE_ANNEES", "7", "2024-01-01");

        var repo = new AgentCarriereRepository(scope.Conn);
        var result = await repo.ResoudreAsync("A-1", "2025-06-01");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal(7, result.Value.AnciennetePriveeAnnees);
    }

    [Fact]
    public async Task AnciennetePrivee_est_null_quand_aucun_attribut_renseigne()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        SeedAgent(scope.Conn, "A-1", "MAT-001", "2015-09-01");
        SeedCarriere(scope.Conn, "C-1", "A-1", "PEM-G1", "13", "5", "2015-09-01");

        var repo = new AgentCarriereRepository(scope.Conn);
        var result = await repo.ResoudreAsync("A-1", "2025-06-01");

        Assert.True(result.IsSuccess);
        // Cas le plus fréquent (IEP_CONT) : pas d'ancienneté privée →
        // abstention, pas un 0 silencieux.
        Assert.Null(result.Value.AnciennetePriveeAnnees);
    }
}
