using Microsoft.Data.Sqlite;
using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Infrastructure.Repositories.Agents;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Shared.Time;
using PaieEducation.Seeding;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Preuve d'intégration de <see cref="ValiderBulletin"/> (Phase 5, tâche 4) :
/// bout-en-bout jusqu'à la ligne <c>Bulletins</c> persistée, et immutabilité
/// (2e validation du même agent/date refusée — ADR-0008).
/// </summary>
public class ValiderBulletinTests
{
    private sealed class HorlogeFixe(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
        public DateTimeOffset UtcNow => now;
        public DateOnly Today => DateOnly.FromDateTime(now.UtcDateTime);
    }

    private static async Task SeedTout(SqliteConnection conn)
    {
        await new ReglementaireSeeder().SeedAsync(conn);
        await new IrgSeeder().SeedAsync(conn);
        await new FormulesSeeder().SeedAsync(conn);
    }

    private static void SeedAgentReel(SqliteConnection conn)
    {
        Exec(conn, """
            INSERT INTO Filieres (Id, Libelle, CreatedAt, Hash) VALUES ('ENSEIGNANT', 'Enseignant', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ('PDLP', 'Prof. École primaire', 'ENSEIGNANT', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('PDLP-G105', 'Professeur de l''Ecole primaire', 'PDLP', 1, '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('13', 13, 'Catégorie 13', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('5', 5, 'Échelon 5', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO ValeurPoint (Id, DateEffet, Valeur, Version, Hash, CreatedAt) VALUES ('VP-PILOTE', '2007-01-01', 45, 'v', 'h', '2026-01-01T00:00:00Z');
            INSERT INTO GrilleIndiciaire (Id, CategorieId, DateEffet, IndiceMin, Version, Hash, CreatedAt) VALUES ('GI-PILOTE', '13', '2020-01-01', 578, 'v', 'h', '2026-01-01T00:00:00Z');
            INSERT INTO IndicesEchelon (Id, EchelonId, DateEffet, Indice, Version, Hash, CreatedAt) VALUES ('IE-PILOTE', '5', '2020-01-01', 100, 'v', 'h', '2026-01-01T00:00:00Z');
            INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, CreatedAt)
                VALUES ('A-PILOTE', 'MAT-PILOTE', 'Test', 'Pilote', '1985-01-01', '2010-09-01', 'M', '2026-01-01T00:00:00Z');
            INSERT INTO Carrieres (Id, AgentId, GradeId, CategorieId, EchelonId, TypeContrat, DateEffet, Motif, CreatedAt)
                VALUES ('C-PILOTE', 'A-PILOTE', 'PDLP-G105', '13', '5', 'STATUTAIRE', '2010-09-01', 'Recrutement', '2026-01-01T00:00:00Z');
            INSERT INTO AgentAttributs (Id, AgentId, Attribut, Valeur, DateEffet, CreatedAt)
                VALUES ('AA-PILOTE', 'A-PILOTE', 'ORIGINE_STATUTAIRE', 'ENSEIGNANT', '2010-09-01', '2026-01-01T00:00:00Z');
            """);
    }

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static ValiderBulletin BuildUseCase(SqliteConnection conn)
    {
        var calculer = new CalculerBulletin(
            new AgentCarriereRepository(conn), new VariableRepository(conn),
            new PayrollReadRepository(conn), new ParametreSystemeRepository(conn),
            SourceValeurResolverFactory.ResolverReel(conn));
        return new ValiderBulletin(
            calculer, new BulletinRepository(conn),
            new HorlogeFixe(new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero)));
    }

    private static CalculerBulletin.Demande Demande() => new(
        AgentId: "A-PILOTE", DatePaie: "2025-06-01",
        SourcesValeur: new Dictionary<string, decimal> { ["PAPP"] = 0.30m },
        ClesBareme: new Dictionary<string, string> { ["CATEGORIE"] = "13" },
        Profil: ProfilFiscal.Standard);

    [Fact]
    public async Task Executer_valide_et_persiste_le_bulletin()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentReel(scope.Conn);

        var useCase = BuildUseCase(scope.Conn);
        var result = await useCase.ExecuterAsync(Demande());

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal(57739.0, SchemaTestSupport.Scalar<double>(
            scope.Conn, "SELECT Net FROM Bulletins WHERE Id = @id;", ("@id", result.Value)));
    }

    [Fact]
    public async Task Executer_deux_fois_pour_le_meme_agent_et_la_meme_date_echoue_la_seconde_fois()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentReel(scope.Conn);

        var useCase = BuildUseCase(scope.Conn);
        var premier = await useCase.ExecuterAsync(Demande());
        Assert.True(premier.IsSuccess);

        var second = await useCase.ExecuterAsync(Demande());

        Assert.True(second.IsFailure);
        Assert.Contains("bulletin", second.Error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
