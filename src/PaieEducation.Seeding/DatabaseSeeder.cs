using Microsoft.Data.Sqlite;
using PaieEducation.Seeding.Models;

namespace PaieEducation.Seeding;

/// <summary>
/// Implémentation de <see cref="IDataSeeder"/> : orchestre le seed complet
/// (nomenclature + réglementaire + IRG + formules) en réutilisant les
/// seeders existants. Idempotent.
/// </summary>
/// <remarks>
/// La nomenclature provient du CSV cascade embarqué (<see cref="SeedCsvProvider"/>) ;
/// les référentiels réglementaire, IRG et formules sont autonomes.
/// </remarks>
public sealed class DatabaseSeeder : IDataSeeder
{
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
