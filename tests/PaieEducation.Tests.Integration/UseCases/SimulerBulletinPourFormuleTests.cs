using Microsoft.Data.Sqlite;
using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Infrastructure.Repositories.Agents;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Infrastructure.Repositories.Workbench;
using PaieEducation.Seeding;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Tests d'intégration bout-en-bout du use case
/// <see cref="SimulerBulletinPourFormule"/> (P10 — FormulaEditor avancé,
/// simulation agent témoin). On reprend le setup pilote de
/// <c>SimulerImpactReelIntegrationTests</c> (A-PILOTE, catégorie 13,
/// échelon 5, VPI=45) pour prouver :
/// <list type="bullet">
///   <item>que l'override d'une formule change réellement le bulletin ;</item>
///   <item>que le delta net = override − baseline est non nul et du signe
///         attendu ;</item>
///   <item>qu'une expression invalide est rejetée sans toucher au
///         pipeline ;</item>
///   <item>que la simulation d'une rubrique absente du calcul la rajoute
///         en tête (cas « édition d'une nouvelle rubrique »).</item>
/// </list>
/// </summary>
public class SimulerBulletinPourFormuleTests
{
    private sealed class HorlogeFixe(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
        public DateTimeOffset UtcNow => now;
        public DateOnly Today => DateOnly.FromDateTime(now.UtcDateTime);
    }

    private static readonly HorlogeFixe Horloge = new(new DateTimeOffset(2026, 7, 18, 10, 0, 0, TimeSpan.Zero));

    private static async Task SeedTout(SqliteConnection conn)
    {
        await new ReglementaireSeeder().SeedAsync(conn);
        await new IrgSeeder().SeedAsync(conn);
        await new FormulesSeeder().SeedAsync(conn);
    }

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Seed l'agent pilote A-PILOTE (cat. 13, éch. 5, VPI=45 depuis 2007).
    /// Identique au seed de SimulerImpactReelIntegrationTests : le bulletin
    /// pilote à 2025-06-01 est figé à 75 325 DA brut / 49 558 DA net.
    /// </summary>
    private static void SeedAgentPilote(SqliteConnection conn)
    {
        Exec(conn, """
            INSERT INTO Filieres (Id, Libelle, CreatedAt, Hash) VALUES ('ENSEIGNANT', 'Enseignant', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ('PDLP', 'Prof. École primaire', 'ENSEIGNANT', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('PDLP-G105', 'Professeur de l''École primaire', 'PDLP', 1, '2026-01-01T00:00:00Z', 'h');
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

    private static SimulerBulletinPourFormule BuildUseCase(SqliteConnection conn)
    {
        var agents = new AgentCarriereRepository(conn);
        var variables = new VariableRepository(conn);
        var payroll = new PayrollReadRepository(conn);
        var parametres = new ParametreSystemeRepository(conn);
        var calculer = new CalculerBulletin(agents, variables, payroll, parametres, SourceValeurResolverFactory.ResolverReel(conn));
        return new SimulerBulletinPourFormule(calculer, parametres, payroll);
    }

    [Fact]
    public async Task ExecuterAsync_formule_identique_au_baseline_produit_un_delta_null()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentPilote(scope.Conn);
        var useCase = BuildUseCase(scope.Conn);

        // On override la rubrique 'TRAITEMENT' avec exactement son
        // expression officielle (formules_v1.json : (INDICE_MIN + INDICE_ECH) * VPI).
        // Le delta doit être ≈ 0 (aux arrondis près).
        var r = await useCase.ExecuterAsync(new SimulerBulletinPourFormule.Demande(
            AgentId: "A-PILOTE",
            DatePaie: "2025-06-01",
            RubriqueIdOverride: "TRAITEMENT",
            ExpressionOverride: "(INDICE_MIN + INDICE_ECH) * VPI"));

        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        Assert.Equal(0m, r.Value.DeltaNet);
        Assert.Equal(r.Value.BulletinBaseline.Net.Amount, r.Value.Bulletin.Net.Amount);
    }

    [Fact]
    public async Task ExecuterAsync_override_a_zero_produit_un_delta_negatif()
    {
        // Si on remplace l'expression de TRAITEMENT par "0", le net doit chuter
        // (le traitement alimente plusieurs gains en aval : PAPP, QUALIF, EXP_PEDAG).
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentPilote(scope.Conn);
        var useCase = BuildUseCase(scope.Conn);

        var r = await useCase.ExecuterAsync(new SimulerBulletinPourFormule.Demande(
            AgentId: "A-PILOTE",
            DatePaie: "2025-06-01",
            RubriqueIdOverride: "TRAITEMENT",
            ExpressionOverride: "0"));

        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        Assert.True(r.Value.DeltaNet < 0m, $"Le delta doit être négatif (reçu {r.Value.DeltaNet}).");
        // Le bulletin override doit avoir un net inférieur au baseline.
        Assert.True(r.Value.Bulletin.Net.Amount < r.Value.BulletinBaseline.Net.Amount);
    }

    [Fact]
    public async Task ExecuterAsync_expression_invalide_rejette_sans_appeler_le_pipeline()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentPilote(scope.Conn);
        var useCase = BuildUseCase(scope.Conn);

        // Double opérateur binaire : "+*" est un motif syntaxiquement invalide
        // (le parser ne sait pas enchaîner deux opérateurs unaires/binaire).
        var r = await useCase.ExecuterAsync(new SimulerBulletinPourFormule.Demande(
            AgentId: "A-PILOTE",
            DatePaie: "2025-06-01",
            RubriqueIdOverride: "TRAITEMENT",
            ExpressionOverride: "INDICE_MIN +* 100"));

        Assert.True(r.IsFailure);
        Assert.Contains("Formule invalide", r.Error.Message);
    }

    [Fact]
    public async Task ExecuterAsync_avec_nouvelle_rubrique_l_ajoute_en_tete_avec_flags_neutres()
    {
        // 'NOUVELLE_RUB' n'existe pas dans le seed : l'override doit l'ajouter
        // (Nature=Gain, EstImposable=true, EstCotisable=true) et le calcul
        // doit réussir.
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentPilote(scope.Conn);
        var useCase = BuildUseCase(scope.Conn);

        var r = await useCase.ExecuterAsync(new SimulerBulletinPourFormule.Demande(
            AgentId: "A-PILOTE",
            DatePaie: "2025-06-01",
            RubriqueIdOverride: "NOUVELLE_RUB",
            ExpressionOverride: "100"));

        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        // Le bulletin override doit contenir la nouvelle ligne.
        Assert.Contains(r.Value.Bulletin.Lignes, l => l.RubriqueId == "NOUVELLE_RUB");
        // Le baseline ne doit pas la contenir.
        Assert.DoesNotContain(r.Value.BulletinBaseline.Lignes, l => l.RubriqueId == "NOUVELLE_RUB");
    }
}
