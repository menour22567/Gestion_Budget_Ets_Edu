using Microsoft.Data.Sqlite;
using PaieEducation.Seeding.Models;

namespace PaieEducation.Seeding;

/// <summary>
/// Implémentation de <see cref="IDataSeeder"/> : orchestre le seed complet
/// (nomenclature + réglementaire + IRG + formules + agents fictifs) en
/// réutilisant les seeders existants. Idempotent.
/// </summary>
/// <remarks>
/// La nomenclature provient du CSV cascade embarqué (<see cref="SeedCsvProvider"/>) ;
/// les référentiels réglementaire, IRG et formules sont autonomes.
/// Les agents fictifs (30 profils couvrant toutes les filières) sont ajoutés
/// en fin de seed pour permettre un test immédiat de l'application.
/// </remarks>
public sealed class DatabaseSeeder : IDataSeeder
{
    /// <summary>Si <c>true</c>, insère les 30 agents fictifs en fin de seed.</summary>
    public bool SeedFakeAgents { get; init; } = true;

    public async Task<SeedReport> SeedAllAsync(SqliteConnection connection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.State != System.Data.ConnectionState.Open)
            throw new InvalidOperationException("La connexion doit être ouverte.");

        var report = new SeedReport(0);

        var rows = await SeedCsvProvider.ReadEmbeddedRowsAsync(ct).ConfigureAwait(false);
        var nom = await new NomenclatureSeeder().SeedAsync(connection, rows, ct).ConfigureAwait(false);
        Merge(report, nom);

        var reg = await new ReglementaireSeeder().SeedAsync(connection, ct).ConfigureAwait(false);
        Merge(report, reg);

        var irg = await new IrgSeeder().SeedAsync(connection, ct).ConfigureAwait(false);
        Merge(report, irg);

        var frm = await new FormulesSeeder().SeedAsync(connection, ct).ConfigureAwait(false);
        Merge(report, frm);

        // Agents fictifs pour test — activé par défaut, désactivable via
        // SeedFakeAgents = false
        if (SeedFakeAgents)
        {
            var agents = await new FakeAgentSeeder().SeedAsync(connection, ct).ConfigureAwait(false);
            Merge(report, agents);
        }

        return report;
    }

    private static void Merge(SeedReport dest, SeedReport src)
    {
        foreach (var t in src.Tables)
        {
            dest.Add(t.Table, t.Lues, t.Inserees);
        }
    }
}
