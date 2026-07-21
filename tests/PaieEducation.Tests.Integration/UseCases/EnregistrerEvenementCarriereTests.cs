using Microsoft.Data.Sqlite;
using PaieEducation.Application.Agents.UseCases;
using PaieEducation.Domain.Agents;
using PaieEducation.Infrastructure.Repositories.Agents;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Preuve d'intégration de <see cref="EnregistrerEvenementCarriere"/>
/// (chantier gestion des agents) — l'événement enregistré est immédiatement
/// résoluble par <see cref="AgentCarriereRepository"/> à sa date d'effet
/// (même critère de preuve que <see cref="CreerAgentTests"/>).
/// </summary>
public class EnregistrerEvenementCarriereTests
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
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('PEM-G2', 'Grade 2', 'PEM', 2, '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('13', 13, 'Catégorie 13', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('14', 14, 'Catégorie 14', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('5', 5, 'Échelon 5', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('6', 6, 'Échelon 6', '2026-01-01T00:00:00Z', 'h');
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
    public async Task Executer_enregistre_une_promotion_immediatement_resoluble_a_sa_date_d_effet()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        var agentId = await CreerAgentAsync(scope.Conn);

        var useCase = new EnregistrerEvenementCarriere(
            new AgentRepository(scope.Conn), new AgentReadRepository(scope.Conn), new HorlogeFixe(DateTimeOffset.UtcNow));
        var demande = new EvenementCarriere(agentId, "PEM-G2", "14", "6", "STATUTAIRE", "2023-01-01", "Promotion de grade");
        var result = await useCase.ExecuterAsync(demande);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);

        var avant = await new AgentCarriereRepository(scope.Conn).ResoudreAsync(agentId, "2022-06-01");
        Assert.True(avant.IsSuccess);
        Assert.Equal("PEM-G1", avant.Value.Grade);

        var apres = await new AgentCarriereRepository(scope.Conn).ResoudreAsync(agentId, "2023-06-01");
        Assert.True(apres.IsSuccess);
        Assert.Equal("PEM-G2", apres.Value.Grade);
        Assert.Equal(14, apres.Value.Categorie);
    }

    [Fact]
    public async Task Executer_type_contrat_invalide_echoue_explicitement_sans_toucher_la_base()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedReferentiel(scope.Conn);
        var agentId = await CreerAgentAsync(scope.Conn);

        var useCase = new EnregistrerEvenementCarriere(
            new AgentRepository(scope.Conn), new AgentReadRepository(scope.Conn), new HorlogeFixe(DateTimeOffset.UtcNow));
        var demande = new EvenementCarriere(agentId, "PEM-G2", "14", "6", "INVALIDE", "2023-01-01", "Test");
        var result = await useCase.ExecuterAsync(demande);

        Assert.True(result.IsFailure);
        Assert.Contains("contrat", result.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1L, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM Carrieres WHERE AgentId = @id;", ("@id", agentId)));
    }
}
