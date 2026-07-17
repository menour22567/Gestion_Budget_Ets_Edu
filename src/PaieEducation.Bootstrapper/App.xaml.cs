using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PaieEducation.Application.DependencyInjection;
using PaieEducation.Infrastructure.DependencyInjection;
using PaieEducation.Persistence.Migrations;
using PaieEducation.Presentation.DependencyInjection;
using PaieEducation.Presentation.Shell;

namespace PaieEducation.Bootstrapper;

/// <summary>
/// Point d'entrée WPF (Composition Root, Phase 6 tâche 1) : assemble
/// Application/Infrastructure/Presentation (<c>AddApplication</c>/
/// <c>AddInfrastructure</c>/<c>AddPresentation</c>, déjà livrés), applique
/// les migrations, puis résout et affiche le Shell. Le type de base est
/// pleinement qualifié pour éviter la collision avec le namespace
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
}
