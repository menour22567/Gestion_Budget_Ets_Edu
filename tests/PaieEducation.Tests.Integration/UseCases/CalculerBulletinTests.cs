using Microsoft.Data.Sqlite;
using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Infrastructure.Repositories.Agents;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Seeding;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Preuve d'intégration de <see cref="CalculerBulletin"/> (Phase 5, tâche 4) :
/// même scénario que <see cref="Calcul.BulletinEndToEndTests"/>, mais orchestré
/// par le use case Application (via les ports <c>IAgentCarriereRepository</c>/
/// <c>IVariableRepository</c>/<c>IPayrollReadRepository</c>) plutôt qu'appelé
/// manuellement étape par étape.
///
/// C2.2/C2.3 : vérifie l'auto-résolution des clés de barème et des sources de valeur
/// (notation agent pour PAPP) depuis le dossier agent — zéro saisie experte.
/// </summary>
public class CalculerBulletinTests
{
    private static async Task SeedTout(SqliteConnection conn)
    {
        await new ReglementaireSeeder().SeedAsync(conn);
        await new IrgSeeder().SeedAsync(conn);
        await new FormulesSeeder().SeedAsync(conn);
    }

    /// <summary>
    /// Seed un agent pilote complet : carrière + attributs (ORIGINE_STATUTAIRE + NOTATION_AGENT)
    /// pour valider l'auto-résolution C2.2 (clés barème) + C2.3 (notation PAPP).
    /// </summary>
    private static void SeedAgentReelAvecNotation(SqliteConnection conn)
    {
        Exec(conn, """
            INSERT INTO Filieres (Id, Libelle, CreatedAt, Hash) VALUES ('ENSEIGNANT', 'Enseignant', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ('PDLP', 'Prof. Ecole primaire', 'ENSEIGNANT', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('PDLP-G105', 'Professeur de l''Ecole primaire', 'PDLP', 1, '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('13', 13, 'Categorie 13', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('5', 5, 'Echelon 5', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO ValeurPoint (Id, DateEffet, Valeur, Version, Hash, CreatedAt) VALUES ('VP-PILOTE', '2007-01-01', 45, 'v', 'h', '2026-01-01T00:00:00Z');
            INSERT INTO GrilleIndiciaire (Id, CategorieId, DateEffet, IndiceMin, Version, Hash, CreatedAt) VALUES ('GI-PILOTE', '13', '2020-01-01', 578, 'v', 'h', '2026-01-01T00:00:00Z');
            INSERT INTO IndicesEchelon (Id, EchelonId, DateEffet, Indice, Version, Hash, CreatedAt) VALUES ('IE-PILOTE', '5', '2020-01-01', 100, 'v', 'h', '2026-01-01T00:00:00Z');
            INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, CreatedAt)
                VALUES ('A-PILOTE', 'MAT-PILOTE', 'Test', 'Pilote', '1985-01-01', '2010-09-01', 'M', '2026-01-01T00:00:00Z');
            INSERT INTO Carrieres (Id, AgentId, GradeId, CategorieId, EchelonId, TypeContrat, DateEffet, Motif, CreatedAt)
                VALUES ('C-PILOTE', 'A-PILOTE', 'PDLP-G105', '13', '5', 'STATUTAIRE', '2010-09-01', 'Recrutement', '2026-01-01T00:00:00Z');
            -- ORIGINE_STATUTAIRE pour éligibilité ISSRP (groupe DNF)
            INSERT INTO AgentAttributs (Id, AgentId, Attribut, Valeur, DateEffet, DateFin, CreatedAt)
                VALUES ('AA-ORIGINE', 'A-PILOTE', 'ORIGINE_STATUTAIRE', 'ENSEIGNANT', '2010-09-01', NULL, '2026-01-01T00:00:00Z');
            -- NOTATION_AGENT pour PAPP (note sur 20) : 15/20 -> taux = 15/20 * 0.40 = 0.30
            INSERT INTO AgentAttributs (Id, AgentId, Attribut, Valeur, DateEffet, DateFin, CreatedAt)
                VALUES ('AA-NOTE', 'A-PILOTE', 'NOTATION_AGENT', '15', '2025-01-01', NULL, '2026-01-01T00:00:00Z');
            """);
    }

    /// <summary>
    /// Seed un agent SANS notation pour vérifier l'abstention ADR-0009 sur PAPP.
    /// </summary>
    private static void SeedAgentSansNotation(SqliteConnection conn)
    {
        Exec(conn, """
            INSERT INTO Filieres (Id, Libelle, CreatedAt, Hash) VALUES ('ENSEIGNANT', 'Enseignant', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ('PDLP', 'Prof. Ecole primaire', 'ENSEIGNANT', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('PDLP-G105', 'Professeur de l''Ecole primaire', 'PDLP', 1, '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('13', 13, 'Categorie 13', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('5', 5, 'Echelon 5', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO ValeurPoint (Id, DateEffet, Valeur, Version, Hash, CreatedAt) VALUES ('VP-PILOTE', '2007-01-01', 45, 'v', 'h', '2026-01-01T00:00:00Z');
            INSERT INTO GrilleIndiciaire (Id, CategorieId, DateEffet, IndiceMin, Version, Hash, CreatedAt) VALUES ('GI-PILOTE', '13', '2020-01-01', 578, 'v', 'h', '2026-01-01T00:00:00Z');
            INSERT INTO IndicesEchelon (Id, EchelonId, DateEffet, Indice, Version, Hash, CreatedAt) VALUES ('IE-PILOTE', '5', '2020-01-01', 100, 'v', 'h', '2026-01-01T00:00:00Z');
            INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, CreatedAt)
                VALUES ('A-SANS-NOTE', 'MAT-002', 'MARTIN', 'Marie', '1985-03-10', '2015-09-01', 'F', '2026-01-01T00:00:00Z');
            INSERT INTO Carrieres (Id, AgentId, GradeId, CategorieId, EchelonId, TypeContrat, DateEffet, Motif, CreatedAt)
                VALUES ('C-SANS-NOTE', 'A-SANS-NOTE', 'PDLP-G105', '13', '5', 'STATUTAIRE', '2015-09-01', 'Recrutement', '2026-01-01T00:00:00Z');
            INSERT INTO AgentAttributs (Id, AgentId, Attribut, Valeur, DateEffet, DateFin, CreatedAt)
                VALUES ('AA-ORIGINE-2', 'A-SANS-NOTE', 'ORIGINE_STATUTAIRE', 'ENSEIGNANT', '2015-09-01', NULL, '2026-01-01T00:00:00Z');
            """);
    }

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Demande SANS saisie manuelle — tout est auto-résolu (C2.2/C2.3).
    /// </summary>
    private static CalculerBulletin.Demande DemandeAuto(string agentId) => new(
        AgentId: agentId,
        DatePaie: "2025-06-01",
        // C2.2/C2.3 : AUCUNE saisie manuelle — tout est auto-résolu depuis le dossier agent
        SourcesValeur: null,
        ClesBareme: null,
        Profil: ProfilFiscal.Standard);

    [Fact]
    public async Task Executer_calcule_le_bulletin_complet_avec_auto_resolution_C2_C3()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentReelAvecNotation(scope.Conn);

        var useCase = new CalculerBulletin(
            new AgentCarriereRepository(scope.Conn),
            new VariableRepository(scope.Conn),
            new PayrollReadRepository(scope.Conn), new ParametreSystemeRepository(scope.Conn),
            SourceValeurResolverFactory.ResolverReel(scope.Conn));

        // C2.2/C2.3 : on ne fournit PAS SourcesValeur ni ClesBareme — ils sont auto-résolus
        var bulletin = await useCase.ExecuterAsync(DemandeAuto("A-PILOTE"));

        Assert.True(bulletin.IsSuccess, bulletin.IsFailure ? bulletin.Error.Message : null);
        // Net attendu avec PAPP résolu via notation auto (15/20 = 0.30 taux)
        Assert.Equal(57739m, bulletin.Value.Net.Amount);
    }

    [Fact]
    public async Task Executer_agent_sans_notation_papp_abstention_ADR009()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        // Agent SANS NOTATION_AGENT -> PAPP doit être non éligible (abstention ADR-0009)
        SeedAgentSansNotation(scope.Conn);

        var useCase = new CalculerBulletin(
            new AgentCarriereRepository(scope.Conn),
            new VariableRepository(scope.Conn),
            new PayrollReadRepository(scope.Conn), new ParametreSystemeRepository(scope.Conn),
            SourceValeurResolverFactory.ResolverReel(scope.Conn));

        var bulletin = await useCase.ExecuterAsync(DemandeAuto("A-SANS-NOTE"));

        Assert.True(bulletin.IsSuccess, bulletin.IsFailure ? bulletin.Error.Message : null);
        // PAPP non résolu -> pas de PAPP dans le bulletin (vérification via lignes)
        Assert.DoesNotContain(bulletin.Value.Lignes, l => l.RubriqueId == "PAPP");
    }

    [Fact]
    public async Task Executer_agent_inexistant_court_circuite_avec_l_erreur_du_repository_agent()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);

        var useCase = new CalculerBulletin(
            new AgentCarriereRepository(scope.Conn),
            new VariableRepository(scope.Conn),
            new PayrollReadRepository(scope.Conn), new ParametreSystemeRepository(scope.Conn),
            SourceValeurResolverFactory.ResolverReel(scope.Conn));

        var bulletin = await useCase.ExecuterAsync(DemandeAuto("A-INEXISTANT"));

        Assert.True(bulletin.IsFailure);
        Assert.Contains("introuvable", bulletin.Error.Message, StringComparison.OrdinalIgnoreCase);
    }
}