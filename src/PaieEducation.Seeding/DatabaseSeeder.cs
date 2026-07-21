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
/// Les agents fictifs de test (<see cref="FakeAgentSeeder"/>) ne sont ajoutés
/// que si <see cref="SeedFakeAgents"/> est explicitement activé (faux par
/// défaut) — jamais sur le chemin de production.
/// </remarks>
public sealed class DatabaseSeeder : IDataSeeder
{
    /// <summary>
    /// Si <c>true</c>, insère les agents fictifs de test (<see cref="FakeAgentSeeder"/>)
    /// en fin de seed. <b>Faux par défaut</b> : le chemin de production ne doit
    /// jamais injecter de données de test. Activé explicitement par le CLI de
    /// test (<c>seed all --with-fake-agents</c>).
    /// </summary>
    public bool SeedFakeAgents { get; init; } = false;

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

        // Agents fictifs de test — désactivés par défaut (jamais en production),
        // activés explicitement par le CLI (seed all --with-fake-agents).
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
