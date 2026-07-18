using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Common;
using PaieEducation.Domain.Workbench.Calculators;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Infrastructure.Persistence;
using PaieEducation.Infrastructure.Repositories.Agents;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Infrastructure.Repositories.Workbench;
using PaieEducation.Infrastructure.Time;
using PaieEducation.Infrastructure.Workbench.Calculators;
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

        services.AddScoped<IUnitOfWork, DapperUnitOfWork>();

        services.AddScoped<IAgentCarriereRepository, AgentCarriereRepository>();
        services.AddScoped<IVariableRepository, VariableRepository>();
        services.AddScoped<IParametreSystemeRepository, ParametreSystemeRepository>();
        services.AddScoped<IPayrollReadRepository, PayrollReadRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<IAgentReadRepository, AgentReadRepository>();
        services.AddScoped<IBulletinRepository, BulletinRepository>();
        services.AddScoped<IBulletinReadRepository, BulletinReadRepository>();
        services.AddScoped<IRappelRepository, RappelRepository>();
        services.AddScoped<IGrilleIndiciaireRepository, GrilleIndiciaireRepository>();
        services.AddScoped<IReferentielReadRepository, ReferentielReadRepository>();
        services.AddScoped<WorkbenchReadRepository>();
        services.AddScoped<IWorkbenchReadRepository>(sp => sp.GetRequiredService<WorkbenchReadRepository>());
        services.AddScoped<WorkbenchReadCache>();
        services.AddScoped<ICacheInvalidator>(sp => sp.GetRequiredService<WorkbenchReadCache>());
        services.AddScoped<IAgentRubriqueRepository, AgentRubriqueRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IRubriqueRepository, RubriqueRepository>();

        // Lot 1.2 — port pour la lecture des paramètres de rubrique
        // (utilisé par ConstanteReglementaireCalculator). Enregistré en
        // Scoped pour partager la connexion avec le reste de l'unité de calcul.
        services.AddScoped<IRubriqueParametreLookup, RubriqueParametreLookup>();

        // C2.3 — SourceValeurResolver (pattern Open/Closed, ADR-0007 D6) : les 7
        // calculateurs de sources sont enregistrés individuellement puis indexés
        // par CodeSource pour construire le resolver.
        services.AddTransient<NotationAgentCalculator>();
        services.AddTransient<AnciennetePubliqueCalculator>();
        services.AddTransient<AnciennetePriveeCalculator>();
        services.AddTransient<IndiceEchelonCalculator>();
        services.AddTransient<PointIndiciaireCalculator>();
        services.AddTransient<BaseAssietteCalculator>();
        // Lot 1.2 — ConstanteReglementaireCalculator a quitté le Domain
        // (besoin d'I/O via IRubriqueParametreLookup). Il vit maintenant
        // dans Infrastructure.Workbench.Calculators et est câblé ici
        // derrière ISourceValeurCalculator, sans changement du moteur.
        services.AddTransient<ConstanteReglementaireCalculator>();
        services.AddTransient<ISourceValeurResolver>(sp =>
        {
            var calculators = new ISourceValeurCalculator[]
            {
                sp.GetRequiredService<NotationAgentCalculator>(),
                sp.GetRequiredService<AnciennetePubliqueCalculator>(),
                sp.GetRequiredService<AnciennetePriveeCalculator>(),
                sp.GetRequiredService<IndiceEchelonCalculator>(),
                sp.GetRequiredService<PointIndiciaireCalculator>(),
                sp.GetRequiredService<BaseAssietteCalculator>(),
                sp.GetRequiredService<ConstanteReglementaireCalculator>(),
            };
            var index = calculators.ToDictionary(c => c.CodeSource, c => c, StringComparer.OrdinalIgnoreCase);
            return new SourceValeurResolver(index);
        });

        return services;
    }
}
