using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Rappels;
using PaieEducation.Infrastructure.Repositories.Payroll;

namespace PaieEducation.Tests.Integration.Repositories;

/// <summary>
/// Tests de <see cref="RappelRepository"/> (Phase 5, tâche 5, D9) : persistance
/// des lignes de rappel et garde d'idempotence (<c>ExisteAsync</c>).
/// </summary>
public class RappelRepositoryTests
{
    private static void SeedAgentMinimal(SqliteConnection c, string agentId) => SchemaTestSupport.Exec(c, """
        INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, CreatedAt)
        VALUES ($id, 'MAT-001', 'Test', 'Agent', '1990-01-01', '2015-09-01', 'M', '2026-01-01T00:00:00Z');
        """, ("$id", agentId));

    [Fact]
    public async Task ExisteAsync_renvoie_faux_avant_toute_generation()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedAgentMinimal(scope.Conn, "A-1");
        var repo = new RappelRepository(scope.Conn);

        var existe = await repo.ExisteAsync("A-1", "2025-06-01");

        Assert.True(existe.IsSuccess);
        Assert.False(existe.Value);
    }

    [Fact]
    public async Task EnregistrerAsync_persiste_une_ligne_par_rubrique_et_ExisteAsync_devient_vrai()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedAgentMinimal(scope.Conn, "A-1");
        var repo = new RappelRepository(scope.Conn);

        var lignes = new List<LigneRappel>
        {
            new("QUALIF", MontantAncien: 2000m, MontantNouveau: 2500m, Delta: 500m),
            new("PAPP", MontantAncien: 1000m, MontantNouveau: 900m, Delta: -100m),
        };

        var enregistre = await repo.EnregistrerAsync(
            "A-1", "2025-06-01", lignes, new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero));

        Assert.True(enregistre.IsSuccess, enregistre.IsFailure ? enregistre.Error.Message : null);

        var nbLignes = SchemaTestSupport.Scalar<long>(
            scope.Conn, "SELECT COUNT(*) FROM Rappels WHERE AgentId = 'A-1' AND DatePaieOrigine = '2025-06-01';");
        Assert.Equal(2, nbLignes);

        var deltaQualif = SchemaTestSupport.Scalar<double>(
            scope.Conn, "SELECT Delta FROM Rappels WHERE AgentId = 'A-1' AND RubriqueId = 'QUALIF';");
        Assert.Equal(500.0, deltaQualif);

        var existe = await repo.ExisteAsync("A-1", "2025-06-01");
        Assert.True(existe.IsSuccess);
        Assert.True(existe.Value);
    }
}
