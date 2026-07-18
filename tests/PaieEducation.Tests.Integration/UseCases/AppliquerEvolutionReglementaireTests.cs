using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Infrastructure.Repositories.Workbench;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Preuve d'intégration de <see cref="AppliquerEvolutionReglementaire"/>
/// (Phase 5, tâche 5, D8 — dernier use case Workbench de la tâche).
/// </summary>
public class AppliquerEvolutionReglementaireTests
{
    private sealed class HorlogeFixe(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
        public DateTimeOffset UtcNow => now;
        public DateOnly Today => DateOnly.FromDateTime(now.UtcDateTime);
    }

    private static readonly HorlogeFixe Horloge = new(new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero));

    private static readonly RapportImpact Rapport = new(
        NbAgents: 1240, DeltaMinMensuel: 500m, DeltaMaxMensuel: 900m, MontantTotalMensuel: 3_100_000m,
        PeriodeImpactee: "2026-01-01", BulletinsAvertis: 0);

    [Fact]
    public async Task Executer_clotureEtNouvelleVersion_ecrit_ValeurPoint_et_une_ligne_AuditLog()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var grille = new GrilleIndiciaireRepository(scope.Conn);
        await grille.DefinirValeurPointAsync(45m, "2007-01-01", "2007", null, DateTimeOffset.UtcNow);

        var useCase = new AppliquerEvolutionReglementaire(grille, new AuditLogRepository(scope.Conn), Horloge, new TestUnitOfWork());
        var demande = new AppliquerEvolutionReglementaire.Demande(
            Description: "Décret 26-XX — revalorisation du point indiciaire",
            RapportImpact: Rapport,
            Strategie: StrategieVersionning.ClotureEtNouvelleVersion,
            NouvelleValeur: 50m,
            DateEffet: "2026-01-01",
            Version: "2026",
            Source: "Décret 26-XX",
            Actor: "admin");

        var result = await useCase.ExecuterAsync(demande);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("VP-2026-01-01", result.Value);
        Assert.Equal(50.0, SchemaTestSupport.Scalar<double>(
            scope.Conn, "SELECT Valeur FROM ValeurPoint WHERE Id = @id;", ("@id", result.Value)));

        Assert.Equal(1, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM AuditLog;"));
        Assert.Equal("admin", SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT Actor FROM AuditLog;"));
        Assert.Equal("ValeurPoint", SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT EntityType FROM AuditLog;"));
        Assert.Equal("VP-2026-01-01", SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT EntityId FROM AuditLog;"));
        var payload = SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT Payload FROM AuditLog;");
        Assert.Contains("1240", payload);
        Assert.Contains("ClotureEtNouvelleVersion", payload);
    }

    [Fact]
    public async Task Executer_duplication_clone_la_valeur_courante_et_ecrit_l_audit()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var grille = new GrilleIndiciaireRepository(scope.Conn);
        await grille.DefinirValeurPointAsync(45m, "2007-01-01", "2007", null, DateTimeOffset.UtcNow);

        var useCase = new AppliquerEvolutionReglementaire(grille, new AuditLogRepository(scope.Conn), Horloge, new TestUnitOfWork());
        var demande = new AppliquerEvolutionReglementaire.Demande(
            Description: "Reconduction du taux 2025 sur 2026",
            RapportImpact: Rapport,
            Strategie: StrategieVersionning.Duplication,
            NouvelleValeur: null,
            DateEffet: "2026-01-01",
            Version: "2026",
            Source: null,
            Actor: "admin");

        var result = await useCase.ExecuterAsync(demande);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal(45.0, SchemaTestSupport.Scalar<double>(
            scope.Conn, "SELECT Valeur FROM ValeurPoint WHERE Id = @id;", ("@id", result.Value)));
        Assert.Equal(1, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM AuditLog;"));
    }

    [Fact]
    public async Task Executer_clotureEtNouvelleVersion_sans_nouvelle_valeur_echoue_sans_rien_ecrire()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var grille = new GrilleIndiciaireRepository(scope.Conn);
        await grille.DefinirValeurPointAsync(45m, "2007-01-01", "2007", null, DateTimeOffset.UtcNow);

        var useCase = new AppliquerEvolutionReglementaire(grille, new AuditLogRepository(scope.Conn), Horloge, new TestUnitOfWork());
        var demande = new AppliquerEvolutionReglementaire.Demande(
            Description: "Test", RapportImpact: Rapport, Strategie: StrategieVersionning.ClotureEtNouvelleVersion,
            NouvelleValeur: null, DateEffet: "2026-01-01", Version: "2026", Source: null, Actor: "admin");

        var result = await useCase.ExecuterAsync(demande);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal(1, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM ValeurPoint;"));
        Assert.Equal(0, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM AuditLog;"));
    }

    [Fact]
    public async Task Executer_ecriture_reglementaire_en_echec_n_ecrit_aucune_ligne_audit()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var grille = new GrilleIndiciaireRepository(scope.Conn);
        await grille.DefinirValeurPointAsync(45m, "2007-01-01", "2007", null, DateTimeOffset.UtcNow);

        var useCase = new AppliquerEvolutionReglementaire(grille, new AuditLogRepository(scope.Conn), Horloge, new TestUnitOfWork());
        var demande = new AppliquerEvolutionReglementaire.Demande(
            Description: "Date déjà utilisée", RapportImpact: Rapport,
            Strategie: StrategieVersionning.ClotureEtNouvelleVersion, NouvelleValeur: 46m,
            DateEffet: "2007-01-01", Version: "2007-bis", Source: null, Actor: "admin");

        var result = await useCase.ExecuterAsync(demande);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(0, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM AuditLog;"));
    }

    [Fact]
    public async Task Executer_sans_rapportImpact_ni_bypass_echoue_sans_rien_ecrire()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var grille = new GrilleIndiciaireRepository(scope.Conn);
        await grille.DefinirValeurPointAsync(45m, "2007-01-01", "2007", null, DateTimeOffset.UtcNow);

        var useCase = new AppliquerEvolutionReglementaire(grille, new AuditLogRepository(scope.Conn), Horloge, new TestUnitOfWork());
        var demande = new AppliquerEvolutionReglementaire.Demande(
            Description: "Sans dry-run", RapportImpact: null, Strategie: StrategieVersionning.Duplication,
            NouvelleValeur: null, DateEffet: "2026-01-01", Version: "2026", Source: null, Actor: "admin");

        var result = await useCase.ExecuterAsync(demande);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal(1, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM ValeurPoint;"));
        Assert.Equal(0, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM AuditLog;"));
    }

    [Fact]
    public async Task Executer_bypass_sans_raison_echoue_sans_rien_ecrire()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var grille = new GrilleIndiciaireRepository(scope.Conn);
        await grille.DefinirValeurPointAsync(45m, "2007-01-01", "2007", null, DateTimeOffset.UtcNow);

        var useCase = new AppliquerEvolutionReglementaire(grille, new AuditLogRepository(scope.Conn), Horloge, new TestUnitOfWork());
        var demande = new AppliquerEvolutionReglementaire.Demande(
            Description: "Bypass sans raison", RapportImpact: null, Strategie: StrategieVersionning.Duplication,
            NouvelleValeur: null, DateEffet: "2026-01-01", Version: "2026", Source: null, Actor: "admin",
            BypassDryRun: true, RaisonBypass: null);

        var result = await useCase.ExecuterAsync(demande);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal(0, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM AuditLog;"));
    }

    [Fact]
    public async Task Executer_bypass_avec_raison_commit_sans_rapportImpact_et_trace_une_action_distincte()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var grille = new GrilleIndiciaireRepository(scope.Conn);
        await grille.DefinirValeurPointAsync(45m, "2007-01-01", "2007", null, DateTimeOffset.UtcNow);

        var useCase = new AppliquerEvolutionReglementaire(grille, new AuditLogRepository(scope.Conn), Horloge, new TestUnitOfWork());
        var demande = new AppliquerEvolutionReglementaire.Demande(
            Description: "Urgence — panne du moteur de simulation", RapportImpact: null,
            Strategie: StrategieVersionning.Duplication, NouvelleValeur: null, DateEffet: "2026-01-01",
            Version: "2026", Source: null, Actor: "admin",
            BypassDryRun: true, RaisonBypass: "Simulation indisponible, validation manuelle par le directeur");

        var result = await useCase.ExecuterAsync(demande);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal(1, SchemaTestSupport.Scalar<long>(scope.Conn, "SELECT COUNT(*) FROM AuditLog;"));
        Assert.Equal("APPLIQUER_EVOLUTION_BYPASS", SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT Action FROM AuditLog;"));
        var payload = SchemaTestSupport.Scalar<string>(scope.Conn, "SELECT Payload FROM AuditLog;");
        Assert.Contains("Simulation indisponible", payload);
    }
}
