using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Infrastructure.Repositories.Agents;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Infrastructure.Repositories.Workbench;
using PaieEducation.Infrastructure.Time;
using PaieEducation.Shared.Time;

namespace PaieEducation.Infrastructure.DependencyInjection;

/// <summary>
/// Composition Root — enregistrement des services Infrastructure
/// (connexion SQLite + implémentations des ports Domain). <c>Bootstrapper</c>
/// décide de la provenance de <paramref name="connectionString"/> (fichier
/// de config, chemin utilisateur...) — Infrastructure reste agnostique.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped(_ =>
        {
            var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys=ON;";
            pragma.ExecuteNonQuery();
            return connection;
        });

        services.AddScoped<IAgentCarriereRepository, AgentCarriereRepository>();
        services.AddScoped<IVariableRepository, VariableRepository>();
        services.AddScoped<IPayrollReadRepository, PayrollReadRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<IBulletinRepository, BulletinRepository>();
        services.AddScoped<IBulletinReadRepository, BulletinReadRepository>();
        services.AddScoped<IGrilleIndiciaireRepository, GrilleIndiciaireRepository>();
        services.AddScoped<IWorkbenchReadRepository, WorkbenchReadRepository>();
        services.AddScoped<IAgentRubriqueRepository, AgentRubriqueRepository>();

        return services;
    }
}
