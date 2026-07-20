using Microsoft.Data.Sqlite;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Infrastructure.Repositories.Agents;
using PaieEducation.Infrastructure.Repositories.Workbench;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Preuve du critère d'acceptation du plan P6 (audit du 19/07/2026) : « une
/// règle DNF peut être créée/fermée/versionnée puis vérifiée par
/// <see cref="SuggererRubriques"/> ». Une condition posée par
/// <see cref="DefinirRegleEligibilite"/> (via une rubrique/agent entièrement
/// synthétiques, sans dépendre du seed ISSRP réel) doit immédiatement
/// influencer la sortie de <see cref="SuggererRubriques"/> — même patron que
/// <c>DefinirValeurBaremeTests.Executer_edition_bout_en_bout_...</c> (P5).
/// </summary>
/// <remarks>
/// <see cref="SuggererRubriques"/> ignore délibérément les conditions
/// communes (<c>GroupeId</c> nul) — cf. sa doc : « limité aux rubriques dont
/// l'éligibilité repose sur au moins un groupe DNF ». Les conditions posées
/// ici référencent donc systématiquement un groupe créé au préalable via
/// <see cref="DefinirGroupeEligibilite"/>.
/// </remarks>
public class DefinirRegleEligibiliteOracleTests
{
    private sealed class HorlogeFixe(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
        public DateTimeOffset UtcNow => now;
        public DateOnly Today => DateOnly.FromDateTime(now.UtcDateTime);
    }

    private static readonly HorlogeFixe Horloge = new(new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero));

    private static void SeedRubriqueAffectable(SqliteConnection c) => SchemaTestSupport.Exec(c, """
        INSERT INTO Rubriques (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul, EstAffectableManuellement, CreatedAt, Hash)
        VALUES ('TEST_DNF', 'Test DNF', 'GAIN', 'TBASE', 'MENSUELLE', 10, 1, '2026-01-01T00:00:00Z', 'h');
        """);

    private static void SeedAgent(SqliteConnection c, string agentId, string gradeId, int ordre) => SchemaTestSupport.Exec(c, $"""
        INSERT INTO Filieres (Id, Libelle, CreatedAt, Hash) VALUES ('TESTFIL', 'Test filière', '2026-01-01T00:00:00Z', 'h')
            ON CONFLICT (Id) DO NOTHING;
        INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ('TESTCORPS', 'Test corps', 'TESTFIL', '2026-01-01T00:00:00Z', 'h')
            ON CONFLICT (Id) DO NOTHING;
        INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('{gradeId}', 'Grade {gradeId}', 'TESTCORPS', {ordre}, '2026-01-01T00:00:00Z', 'h')
            ON CONFLICT (Id) DO NOTHING;
        INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('10', 10, 'Catégorie 10', '2026-01-01T00:00:00Z', 'h')
            ON CONFLICT (Id) DO NOTHING;
        INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('3', 3, 'Échelon 3', '2026-01-01T00:00:00Z', 'h')
            ON CONFLICT (Id) DO NOTHING;
        INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, CreatedAt)
            VALUES ('{agentId}', 'MAT-{agentId}', 'Test', '{agentId}', '1985-01-01', '2010-09-01', 'M', '2026-01-01T00:00:00Z');
        INSERT INTO Carrieres (Id, AgentId, GradeId, CategorieId, EchelonId, TypeContrat, DateEffet, Motif, CreatedAt)
            VALUES ('C-{agentId}', '{agentId}', '{gradeId}', '10', '3', 'STATUTAIRE', '2010-09-01', 'Recrutement', '2026-01-01T00:00:00Z');
        """);

    private static async Task<string> SeedGroupeAsync(SqliteConnection conn, IClock horloge)
    {
        var definirGroupe = new DefinirGroupeEligibilite(new GroupeEligibiliteRepository(conn), horloge);
        var groupe = await definirGroupe.ExecuterAsync(new DefinirGroupeEligibilite.Demande(
            "GE-TEST", "TEST_DNF", "INFO", null, 100, "2020-01-01", null, null, "workbench"));
        Assert.True(groupe.IsSuccess, groupe.IsFailure ? groupe.Error.Message : null);
        return groupe.Value;
    }

    private static SuggererRubriques BuildSuggererRubriques(SqliteConnection conn) => new(
        new AgentCarriereRepository(conn), new WorkbenchReadRepository(conn),
        new AgentRubriqueRepository(conn), Horloge);

    [Fact]
    public async Task Regle_creee_via_l_editeur_est_immediatement_vue_par_SuggererRubriques()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubriqueAffectable(scope.Conn);
        SeedAgent(scope.Conn, "A-CREE", "G-TEST", ordre: 1);
        var groupeId = await SeedGroupeAsync(scope.Conn, Horloge);
        var definir = new DefinirRegleEligibilite(new RegleEligibiliteRepository(scope.Conn), Horloge);

        var definition = await definir.ExecuterAsync(new DefinirRegleEligibilite.Demande(
            "TEST_DNF", "GRADE", groupeId, "=", "G-TEST", "2020-01-01"));
        Assert.True(definition.IsSuccess, definition.IsFailure ? definition.Error.Message : null);

        var suggestion = await BuildSuggererRubriques(scope.Conn).ExecuterAsync(
            new SuggererRubriques.Demande("A-CREE", "2026-06-01"));

        Assert.True(suggestion.IsSuccess, suggestion.IsFailure ? suggestion.Error.Message : null);
        Assert.Contains("TEST_DNF", suggestion.Value);
    }

    [Fact]
    public async Task Regle_versionnee_change_la_condition_a_compter_de_la_nouvelle_date_effet()
    {
        // v1 (2020-01-01..2025-12-31, fermée automatiquement) : GRADE = G-V1.
        // v2 (2026-01-01..) : GRADE = G-V2. Un agent G-V1 est éligible avant
        // 2026, plus après ; un agent G-V2 est l'inverse — la preuve que
        // DefinirRegleEligibilite verse une nouvelle version consommée par
        // SuggererRubriques à la bonne date, pas seulement en base.
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubriqueAffectable(scope.Conn);
        SeedAgent(scope.Conn, "A-V1", "G-V1", ordre: 1);
        SeedAgent(scope.Conn, "A-V2", "G-V2", ordre: 2);
        var groupeId = await SeedGroupeAsync(scope.Conn, Horloge);
        var definir = new DefinirRegleEligibilite(new RegleEligibiliteRepository(scope.Conn), Horloge);
        await definir.ExecuterAsync(new DefinirRegleEligibilite.Demande(
            "TEST_DNF", "GRADE", groupeId, "=", "G-V1", "2020-01-01"));

        var revision = await definir.ExecuterAsync(new DefinirRegleEligibilite.Demande(
            "TEST_DNF", "GRADE", groupeId, "=", "G-V2", "2026-01-01", "Décret révisé"));
        Assert.True(revision.IsSuccess, revision.IsFailure ? revision.Error.Message : null);

        var suggerer = BuildSuggererRubriques(scope.Conn);
        var v1AvantRevision = await suggerer.ExecuterAsync(new SuggererRubriques.Demande("A-V1", "2025-06-01"));
        var v1ApresRevision = await suggerer.ExecuterAsync(new SuggererRubriques.Demande("A-V1", "2026-06-01"));
        var v2ApresRevision = await suggerer.ExecuterAsync(new SuggererRubriques.Demande("A-V2", "2026-06-01"));

        Assert.Contains("TEST_DNF", v1AvantRevision.Value);
        Assert.DoesNotContain("TEST_DNF", v1ApresRevision.Value);
        Assert.Contains("TEST_DNF", v2ApresRevision.Value);
    }

    [Fact]
    public async Task Regle_close_sans_remplacement_l_agent_n_est_plus_suggere()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedRubriqueAffectable(scope.Conn);
        SeedAgent(scope.Conn, "A-FERMEE", "G-TEST", ordre: 1);
        var groupeId = await SeedGroupeAsync(scope.Conn, Horloge);
        var regles = new RegleEligibiliteRepository(scope.Conn);
        var definir = new DefinirRegleEligibilite(regles, Horloge);
        var definition = await definir.ExecuterAsync(new DefinirRegleEligibilite.Demande(
            "TEST_DNF", "GRADE", groupeId, "=", "G-TEST", "2020-01-01"));
        Assert.True(definition.IsSuccess, definition.IsFailure ? definition.Error.Message : null);

        var suggerer = BuildSuggererRubriques(scope.Conn);
        var avantCloture = await suggerer.ExecuterAsync(new SuggererRubriques.Demande("A-FERMEE", "2025-06-01"));
        Assert.Contains("TEST_DNF", avantCloture.Value);

        var cloture = await new CloreRegleEligibilite(regles).ExecuterAsync(
            new CloreRegleEligibilite.Demande(definition.Value, "2025-12-31"));
        Assert.True(cloture.IsSuccess, cloture.IsFailure ? cloture.Error.Message : null);

        var apresCloture = await suggerer.ExecuterAsync(new SuggererRubriques.Demande("A-FERMEE", "2026-06-01"));
        Assert.DoesNotContain("TEST_DNF", apresCloture.Value);
    }
}
