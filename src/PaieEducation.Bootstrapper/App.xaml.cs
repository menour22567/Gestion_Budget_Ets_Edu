using System.IO;
using System.Windows;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PaieEducation.Application.DependencyInjection;
using PaieEducation.Infrastructure.DependencyInjection;
using PaieEducation.Persistence.Migrations;
using PaieEducation.Presentation.DependencyInjection;
using PaieEducation.Presentation.Shell;
using PaieEducation.Reporting;
using PaieEducation.Seeding;

namespace PaieEducation.Bootstrapper;

/// <summary>
/// Point d'entrée WPF (Composition Root, Phase 6 tâche 1) : assemble
/// Application/Infrastructure/Presentation (<c>AddApplication</c>/
/// <c>AddInfrastructure</c>/<c>AddPresentation</c>, déjà livrés), applique
/// les migrations, puis **auto-initialise** la base si elle est neuve
/// (C1 : seed au 1er lancement), et résout/affiche le Shell. Le type de base
/// est pleinement qualifié pour éviter la collision avec le namespace
/// <c>PaieEducation.Application</c>.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

        var connectionString = builder.Configuration.GetConnectionString("PaieEducation") ?? CheminBaseParDefaut();

        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(connectionString);
        builder.Services.AddPresentation();
        builder.Services.AddReporting();

        _host = builder.Build();

        var migration = AppliquerMigrations(connectionString);
        if (migration.IsFailure)
        {
            MessageBox.Show(
                $"Échec de la migration de la base de données :{Environment.NewLine}{migration.Error}",
                "PaieEducation — erreur de démarrage", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        // C1.2 — Auto-initialisation : une base neuve (migrée mais vide de
        // référentiels) est seedée idempotemment au 1er lancement.
        var seed = InitialiserSiNecessaire(connectionString);
        if (seed.IsFailure)
        {
            MessageBox.Show(
                $"Échec de l'initialisation des données :{Environment.NewLine}{seed.Error}",
                "PaieEducation — erreur de démarrage", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        // C1.3 — Agent de démonstration optionnel (flag off par défaut).
        var demo = SeedAgentDemoSiDemande(connectionString, builder.Configuration);
        if (demo.IsFailure)
        {
            MessageBox.Show(
                $"Échec du seed de l'agent de démonstration :{Environment.NewLine}{demo.Error}",
                "PaieEducation — avertissement", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        var shell = _host.Services.GetRequiredService<ShellWindow>();
        shell.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }

    private static string CheminBaseParDefaut()
    {
        var dossier = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PaieEducation");
        Directory.CreateDirectory(dossier);
        return $"Data Source={Path.Combine(dossier, "paie.db")}";
    }

    private static PaieEducation.Shared.Results.Result<int> AppliquerMigrations(string connectionString)
    {
        var migrator = new SqliteMigrator(
            new SqliteMigratorOptions(connectionString, "bootstrapper"),
            MigrationLoader.LoadFromAssembly(typeof(SqliteMigrator).Assembly, "PaieEducation.Persistence.Migrations."));
        return migrator.Apply();
    }

    /// <summary>
    /// Détecte une base « fraîche » (schéma migré mais sans référentiel) et la
    /// seede. Si des rubriques existent déjà, ne rien refaire (idempotence).
    /// </summary>
    private static PaieEducation.Shared.Results.Result<int> InitialiserSiNecessaire(string connectionString)
    {
        try
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys=ON;";
            pragma.ExecuteNonQuery();

            var total = CompterRubriques(connection);
            if (total > 0)
            {
                return PaieEducation.Shared.Results.Result.Success(0);
            }

            var seeder = new DatabaseSeeder();
            var report = seeder.SeedAllAsync(connection).GetAwaiter().GetResult();
            var inserees = 0;
            foreach (var t in report.Tables) inserees += t.Inserees;
            return PaieEducation.Shared.Results.Result.Success(inserees);
        }
        catch (Exception ex)
        {
            return PaieEducation.Shared.Results.Result.Failure<int>(
                PaieEducation.Shared.Results.Error.Failure($"Échec du seed : {ex.Message}"));
        }
    }

    private static long CompterRubriques(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Rubriques;";
        var v = cmd.ExecuteScalar();
        return v is null or DBNull ? 0L : Convert.ToInt64(v, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// C1.3 — Seed optionnel de l'agent de démonstration si la clé
    /// <c>Seed:DemoAgent</c> est à <c>true</c> dans la configuration
    /// (appsettings.json). Désactivé par défaut. Idempotent.
    /// </summary>
    private static PaieEducation.Shared.Results.Result<int> SeedAgentDemoSiDemande(
        string connectionString, Microsoft.Extensions.Configuration.IConfiguration config)
    {
        var active = config.GetValue("Seed:DemoAgent", false);
        if (!active) return PaieEducation.Shared.Results.Result.Success(0);

        try
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys=ON;";
            pragma.ExecuteNonQuery();

            var seeder = new DemoAgentSeeder();
            var report = seeder.SeedAsync(connection).GetAwaiter().GetResult();
            var inserees = 0;
            foreach (var t in report.Tables) inserees += t.Inserees;
            return PaieEducation.Shared.Results.Result.Success(inserees);
        }
        catch (Exception ex)
        {
            return PaieEducation.Shared.Results.Result.Failure<int>(
                PaieEducation.Shared.Results.Error.Failure($"Échec du seed démo : {ex.Message}"));
        }
    }
}
