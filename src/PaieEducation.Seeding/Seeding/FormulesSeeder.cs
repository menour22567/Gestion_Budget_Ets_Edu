using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;

namespace PaieEducation.Seeding;

/// <summary>
/// Seed des <b>formules de calcul</b> du pilote enseignant (J4.c) -
/// <c>RubriqueFormules</c>. Les expressions sont stockees en base (texte lu
/// par le FormulaEngine), jamais codees en dur dans le moteur (ADR-0005,
/// critere « 0 regle reglementaire dans le code »).
/// </summary>
/// <remarks>
/// <para>Sources : catalogue des formules J3C §1-2.</para>
/// <para><b>Lot 1.3 finalisation</b> : la rubrique TRAITEMENT et les 6
/// formules sont lues depuis <c>Donnees/Formules/formules_v1.json</c>
/// (ressource embarquee, cf. <see cref="FormulesJsonDataReader"/>).
/// Meme pattern que les baremes IRG (Lot 1.3 alpha) : hash SHA-256
/// canonique detecte tout drift entre le JSON et la base.</para>
/// <para>Idempotent (<c>ON CONFLICT DO NOTHING</c>).</para>
/// </remarks>
public sealed class FormulesSeeder
{
    public async Task<SeedReport> SeedAsync(SqliteConnection conn, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(conn);
        if (conn.State != System.Data.ConnectionState.Open)
            throw new InvalidOperationException("La connexion doit etre ouverte.");

        var report = new SeedReport(0);
        await InsertTraitementRubriqueAsync(conn, report, ct).ConfigureAwait(false);
        await InsertFormulesAsync(conn, report, ct).ConfigureAwait(false);
        return report;
    }

    // TRAITEMENT - traitement mensuel de base (TRT), ligne principale du bulletin.
    // Non affectable manuellement (V-2, J4E § 5) : base structurelle du
    // bulletin - la supprimer priverait l'agent de tout salaire.
    private static async Task InsertTraitementRubriqueAsync(SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var data = FormulesJsonDataReader.Load();
        var trait = data.RubriqueTraitement;
        var hash = FormulesJsonDataReader.HashLigne(new
        {
            trait.Id, trait.Libelle, trait.Description,
        });

        var n = await c.ExecuteAsync("""
            INSERT INTO Rubriques
                (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul,
                 EstImposable, EstCotisable, Description, Actif, CreatedAt, Source, Hash,
                 EstAffectableManuellement, OccurrencesMultiples)
            VALUES
                ($id, $l, 'GAIN', 'INDICE_ECHELON',
                 'MENSUELLE', 100, 1, 1,
                 $desc,
                 1, $at, $src, $h, 0, 0)
            ON CONFLICT(Id) DO NOTHING;
            """,
            new
            {
                id = trait.Id,
                l = trait.Libelle,
                desc = trait.Description,
                src = trait.Source,
                h = hash,
                at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            });
        r.Add("Rubriques (TRAITEMENT)", 1, n);
    }

    private static async Task InsertFormulesAsync(SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var data = FormulesJsonDataReader.Load();
        var dateEffet = data.DateEffet;

        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var f in data.Formules)
        {
            ct.ThrowIfCancellationRequested();
            var hash = FormulesJsonDataReader.HashLigne(new
            {
                f.RubriqueId, f.Expression, dateEffet, f.Source,
            });
            var n = await c.ExecuteAsync("""
                INSERT INTO RubriqueFormules
                    (Id, RubriqueId, DateEffet, DateFin, Expression, Ordre, Source, Hash, CreatedAt)
                VALUES
                    ($id, $r, $de, NULL, $e, 0, $src, $h, $at)
                ON CONFLICT(Id) DO NOTHING;
                """,
                new
            {
                id = $"RF-{f.RubriqueId}-{dateEffet}",
                r = f.RubriqueId, de = dateEffet, e = f.Expression, src = f.Source,
                h = hash,
                at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            }, tx);
            inserted += n;
        }
        tx.Commit();
        r.Add("RubriqueFormules", data.Formules.Count, inserted);
    }
}
