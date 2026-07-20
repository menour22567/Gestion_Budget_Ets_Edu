using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Rappels;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Shared.Money;

namespace PaieEducation.Tests.Integration.Repositories;

/// <summary>
/// Tests de <see cref="RappelRepository"/> (Phase 5, tâche 5, D9) : persistance
/// des lignes de rappel et garde d'idempotence (<c>ExisteAsync</c>).
/// </summary>
public class RappelRepositoryTests
{
    private static void SeedAgentMinimal(SqliteConnection c, string agentId) => SchemaTestSupport.Exec(c, """
        INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, CreatedAt)
        VALUES ($id, $matricule, 'Test', 'Agent', '1990-01-01', '2015-09-01', 'M', '2026-01-01T00:00:00Z');
        """, ("$id", agentId), ("$matricule", $"MAT-{agentId}"));

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
            new("QUALIF", MontantAncien: new Money(2000m), MontantNouveau: new Money(2500m), Delta: new Money(500m)),
            new("PAPP", MontantAncien: new Money(1000m), MontantNouveau: new Money(900m), Delta: new Money(-100m)),
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

    [Fact]
    public async Task ListerAsync_sans_rappel_renvoie_une_liste_vide()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedAgentMinimal(scope.Conn, "A-1");
        var repo = new RappelRepository(scope.Conn);

        var lignes = await repo.ListerAsync("A-1", "2025-06-01");

        Assert.True(lignes.IsSuccess);
        Assert.Empty(lignes.Value);
    }

    [Fact]
    public async Task ListerAsync_renvoie_les_lignes_enregistrees_triees_par_rubrique()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedAgentMinimal(scope.Conn, "A-1");
        var repo = new RappelRepository(scope.Conn);
        var enregistrees = new List<LigneRappel>
        {
            new("QUALIF", MontantAncien: new Money(2000m), MontantNouveau: new Money(2500m), Delta: new Money(500m)),
            new("PAPP", MontantAncien: new Money(1000m), MontantNouveau: new Money(900m), Delta: new Money(-100m)),
        };
        await repo.EnregistrerAsync("A-1", "2025-06-01", enregistrees, new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero));

        var lignes = await repo.ListerAsync("A-1", "2025-06-01");

        Assert.True(lignes.IsSuccess);
        Assert.Equal(2, lignes.Value.Count);
        // Tri par RubriqueId (ordre déterministe) : PAPP avant QUALIF.
        Assert.Equal("PAPP", lignes.Value[0].RubriqueId);
        Assert.Equal(-100m, lignes.Value[0].Delta.Amount);
        Assert.Equal("QUALIF", lignes.Value[1].RubriqueId);
        Assert.Equal(2000m, lignes.Value[1].MontantAncien.Amount);
        Assert.Equal(2500m, lignes.Value[1].MontantNouveau.Amount);
        Assert.Equal(500m, lignes.Value[1].Delta.Amount);
    }

    [Fact]
    public async Task ListerAsync_ne_renvoie_pas_les_rappels_d_un_autre_agent_ou_d_une_autre_date()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedAgentMinimal(scope.Conn, "A-1");
        SeedAgentMinimal(scope.Conn, "A-2");
        var repo = new RappelRepository(scope.Conn);
        var lignes = new List<LigneRappel> { new("QUALIF", new Money(2000m), new Money(2500m), new Money(500m)) };
        await repo.EnregistrerAsync("A-1", "2025-06-01", lignes, DateTimeOffset.UtcNow);
        await repo.EnregistrerAsync("A-2", "2025-06-01", lignes, DateTimeOffset.UtcNow);
        await repo.EnregistrerAsync("A-1", "2025-07-01", lignes, DateTimeOffset.UtcNow);

        var resultat = await repo.ListerAsync("A-1", "2025-06-01");

        Assert.True(resultat.IsSuccess);
        Assert.Single(resultat.Value);
    }
}
