using Microsoft.Data.Sqlite;
using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Infrastructure.Repositories.Agents;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Shared.Time;
using PaieEducation.Seeding;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Preuve d'intégration de <see cref="GenererRappels"/> (Phase 5, tâche 5, D9) :
/// bout-en-bout jusqu'à la table <c>Rappels</c>, sur le même agent pilote que
/// <see cref="ValiderBulletinTests"/>/<see cref="CalculerBulletinTests"/>.
/// Portée volontairement réduite (voir mémoire phase5-genererrappels) :
/// un agent + un bulletin déjà validé à la fois.
/// </summary>
public class GenererRappelsTests
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

    private static void SimulerEvolutionRetroactiveValeurPoint(SqliteConnection conn) => Exec(conn, """
        INSERT INTO ValeurPoint (Id, DateEffet, Valeur, Version, Hash, CreatedAt)
        VALUES ('VP-RETRO', '2025-01-01', 50, 'v', 'h', '2026-07-17T00:00:00Z');
        """);

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static CalculerBulletin.Demande Demande() => new(
        AgentId: "A-PILOTE", DatePaie: "2025-06-01",
        SourcesValeur: new Dictionary<string, decimal> { ["PAPP"] = 0.30m },
        ClesBareme: new Dictionary<string, string> { ["CATEGORIE"] = "13" },
        Profil: ProfilFiscal.Standard);

    private static (GenererRappels UseCase, CalculerBulletin Calculer, ValiderBulletin Valider) BuildUseCases(
        SqliteConnection conn, IClock horloge)
    {
        var agents = new AgentCarriereRepository(conn);
        var variables = new VariableRepository(conn);
        var payroll = new PayrollReadRepository(conn);
        var parametres = new ParametreSystemeRepository(conn);
        var bulletinsEcriture = new BulletinRepository(conn);
        var bulletinsLecture = new BulletinReadRepository(conn);
        var rappels = new RappelRepository(conn);
        var calculer = new CalculerBulletin(agents, variables, payroll, parametres, SourceValeurResolverFactory.ResolverReel());

        return (
            new GenererRappels(bulletinsLecture, rappels, calculer, horloge),
            calculer,
            new ValiderBulletin(calculer, bulletinsEcriture, horloge));
    }

    [Fact]
    public async Task Executer_apres_une_evolution_retroactive_genere_et_persiste_les_lignes_de_rappel()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentReel(scope.Conn);
        var horloge = new HorlogeFixe(new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero));
        var (rappelUseCase, calculerUseCase, validerUseCase) = BuildUseCases(scope.Conn, horloge);

        var valide = await validerUseCase.ExecuterAsync(Demande());
        Assert.True(valide.IsSuccess, valide.IsFailure ? valide.Error.Message : null);

        SimulerEvolutionRetroactiveValeurPoint(scope.Conn);

        // Preuve indépendante : un CalculerBulletin frais (hors GenererRappels)
        // donne un Net différent — l'évolution a bien un effet mesurable.
        var recalculAttendu = await calculerUseCase.ExecuterAsync(Demande());
        Assert.True(recalculAttendu.IsSuccess, recalculAttendu.IsFailure ? recalculAttendu.Error.Message : null);
        Assert.NotEqual(57739m, recalculAttendu.Value.Net.Amount);

        var lignes = await rappelUseCase.ExecuterAsync(Demande());

        Assert.True(lignes.IsSuccess, lignes.IsFailure ? lignes.Error.Message : null);
        Assert.NotEmpty(lignes.Value);

        var nbRappels = SchemaTestSupport.Scalar<long>(
            scope.Conn, "SELECT COUNT(*) FROM Rappels WHERE AgentId = 'A-PILOTE' AND DatePaieOrigine = '2025-06-01';");
        Assert.Equal(lignes.Value.Count, (int)nbRappels);

        foreach (var ligne in lignes.Value)
        {
            var deltaPersiste = SchemaTestSupport.Scalar<double>(
                scope.Conn,
                "SELECT Delta FROM Rappels WHERE AgentId = 'A-PILOTE' AND RubriqueId = @rubriqueId;",
                ("@rubriqueId", ligne.RubriqueId));
            Assert.Equal((double)ligne.Delta.Amount, deltaPersiste, precision: 6);
        }
    }

    [Fact]
    public async Task Executer_une_deuxieme_fois_pour_le_meme_bulletin_est_refuse_par_idempotence()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentReel(scope.Conn);
        var horloge = new HorlogeFixe(new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero));
        var (rappelUseCase, _, validerUseCase) = BuildUseCases(scope.Conn, horloge);

        Assert.True((await validerUseCase.ExecuterAsync(Demande())).IsSuccess);
        SimulerEvolutionRetroactiveValeurPoint(scope.Conn);

        var premier = await rappelUseCase.ExecuterAsync(Demande());
        Assert.True(premier.IsSuccess, premier.IsFailure ? premier.Error.Message : null);
        Assert.NotEmpty(premier.Value);

        var second = await rappelUseCase.ExecuterAsync(Demande());

        Assert.True(second.IsFailure);
        Assert.Equal("conflict", second.Error.Code);
    }

    [Fact]
    public async Task Executer_sans_evolution_reglementaire_ne_genere_ni_ne_persiste_aucun_rappel()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentReel(scope.Conn);
        var horloge = new HorlogeFixe(new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero));
        var (rappelUseCase, _, validerUseCase) = BuildUseCases(scope.Conn, horloge);

        Assert.True((await validerUseCase.ExecuterAsync(Demande())).IsSuccess);

        var lignes = await rappelUseCase.ExecuterAsync(Demande());

        Assert.True(lignes.IsSuccess, lignes.IsFailure ? lignes.Error.Message : null);
        Assert.Empty(lignes.Value);

        var nbRappels = SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM Rappels;");
        Assert.Equal(0, nbRappels);
    }

    [Fact]
    public async Task Executer_sans_bulletin_valide_echoue_avec_not_found()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentReel(scope.Conn);
        var horloge = new HorlogeFixe(new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero));
        var (rappelUseCase, _, _) = BuildUseCases(scope.Conn, horloge);

        var lignes = await rappelUseCase.ExecuterAsync(Demande());

        Assert.True(lignes.IsFailure);
        Assert.Equal("not_found", lignes.Error.Code);
    }
}
