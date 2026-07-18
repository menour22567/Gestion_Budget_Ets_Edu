using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;

namespace PaieEducation.Seeding;

/// <summary>
/// Seeder du référentiel IRG.
/// </summary>
/// <remarks>
/// <para>Sources :</para>
/// <list type="bullet">
///   <item><c>Reglementation/IRG_Algerie_2008_2026/CALCUL IRG ALGERIE.txt</c></item>
///   <item><c>Reglementation/IRG_Algerie_2008_2026/EXPLICATION BAREME IRG 2020 APPL (2).docx</c></item>
///   <item><c>Reglementation/IRG_Algerie_2008_2026/IRG_Algerie_2008_2026_PseudoCode.txt</c>
///         (étape 1 : « à partir de 2022-01-01 ⇒ nouveau barème »)</item>
///   <item><c>Reglementation/IRG_Algerie_2008_2026/evolution_bareme_irg_algerie_2008_2026.html</c>
///         (barème LF 2022, Art. 31 → Art. 104 CIDTA)</item>
///   <item><c>docs/PLAN_ACTION.md</c> Q4, Q5, et décision <b>Q-01 du 14/07/2026</b>
///         (révise Q4b : la période 2022+ pointe le barème IRG-2022, pas IRG-2008)</item>
/// </list>
/// <para>Idempotent (<c>INSERT OR IGNORE</c>). Une base seedée avant Q-01 doit
/// être reconstruite (le conflit d'Id laisse l'ancienne règle en place).</para>
/// <para><b>Lot 1.3α</b> : les barèmes 2008/2022 et leurs 10 tranches sont
/// lus depuis <c>Donnees/IRG/baremes_irg_v1.json</c> (ressource embarquée),
/// plus codés en dur. Le hash SHA-256 de chaque ligne permet la détection
/// de drift. Les <c>IRGReglesPeriode</c> (fractions, lissages) restent
/// en C# pour V1 — chantier suivant.</para>
/// </remarks>
public sealed class IrgSeeder
{
    /// <summary>
    /// Insère le barème 2008 + les 4 règles de période. La base doit être
    /// migrée (V006/V007 appliquées).
    /// </summary>
    public async Task<SeedReport> SeedAsync(SqliteConnection conn, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(conn);
        if (conn.State != System.Data.ConnectionState.Open)
            throw new InvalidOperationException("La connexion doit être ouverte.");

        var report = new SeedReport(0);
        await InsertBaremeAsync(conn, report, ct).ConfigureAwait(false);
        await InsertTranchesAsync(conn, report, ct).ConfigureAwait(false);
        await InsertReglesPeriodeAsync(conn, report, ct).ConfigureAwait(false);
        return report;
    }

    // -------------------------------------------------------------------------
    // Barèmes (en-têtes) + tranches — lus depuis le JSON embarqué (Lot 1.3α)
    // -------------------------------------------------------------------------
    private static async Task InsertBaremeAsync(SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var data = BaremeIrgDataReader.Load();

        const string sql = """
            INSERT INTO BaremeIRG (Id, Code, Libelle, DateEffet, DateFin, Source, Hash, CreatedAt)
            VALUES ($id, $c, $l, $de, $df, $src, $h, $at)
            ON CONFLICT(Id) DO NOTHING;
            """;
        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var b in data.Baremes)
        {
            ct.ThrowIfCancellationRequested();
            var hash = BaremeIrgDataReader.HashLigne(new
            {
                b.Id, b.Code, b.Libelle, b.DateEffet, b.DateFin, b.Source,
                TrancheCount = b.Tranches.Count,
            });
            var n = await c.ExecuteAsync(sql, new
            {
                id = b.Id, c = b.Code, l = b.Libelle,
                de = b.DateEffet, df = b.DateFin ?? (object)DBNull.Value,
                src = b.Source, h = hash,
                at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            }, tx);
            inserted += n;
        }
        tx.Commit();
        r.Add("BaremeIRG", data.Baremes.Count, inserted);
    }

    private static async Task InsertTranchesAsync(SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var data = BaremeIrgDataReader.Load();
        var totalTranches = data.Baremes.Sum(b => b.Tranches.Count);

        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var b in data.Baremes)
        {
            foreach (var tr in b.Tranches)
            {
                ct.ThrowIfCancellationRequested();
                var hash = BaremeIrgDataReader.HashLigne(new
                {
                    tr.Id, tr.BorneInf, tr.BorneSup, tr.Taux, tr.Ordre, tr.Source,
                });
                var n = await c.ExecuteAsync("""
                    INSERT INTO BaremeIRGTranches
                        (Id, BaremeId, BorneInf, BorneSup, Taux, Ordre, Source, Hash, CreatedAt)
                    VALUES
                        ($id, $b, $bi, $bs, $taux, $o, $src, $h, $at)
                    ON CONFLICT(Id) DO NOTHING;
                    """,
                    new
                    {
                        id = tr.Id, b = b.Id, bi = tr.BorneInf,
                        bs = tr.BorneSup ?? (object)DBNull.Value,
                        taux = tr.Taux, o = tr.Ordre,
                        src = tr.Source, h = hash,
                        at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                    }, tx);
                inserted += n;
            }
        }
        tx.Commit();
        r.Add("BaremeIRGTranches", totalTranches, inserted);
    }

    // -------------------------------------------------------------------------
    // IRGReglesPeriode — 4 règles (avant 2020-06, 2020-06..12, 2021, 2022+)
    // Hors scope Lot 1.3α : trop spécifique (fractions, lissages) pour un
    // JSON plat. Chantier suivant.
    // -------------------------------------------------------------------------
    private static async Task InsertReglesPeriodeAsync(SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var regles = new (string Id, string Code, string Libelle, string DateDebut, string? DateFin,
            string BaremeId, int Exon, int PlafondSpe, double TauxAbattement, int MinAbattement, int MaxAbattement,
            string CoefG, string ConstG, string CoefS, string ConstS, string Source)[]
        {
            ("IRG-PER-AV-2020-06", "IRG-PER-AV-2020-06",
             "IRG — barème 2008 (sans exonération, sans lissage)",
             "1000-01-01", "2020-05-31", "IRG-2008",
             0, 0, 0.40, 1000, 1500, "1", "0", "1", "0",
             "Barème 2008 — application directe"),
            ("IRG-PER-2020-06", "IRG-PER-2020-06",
             "IRG — barème 2008 + exonération 30 000 + lissage 8/3 (plafond spé 40 000)",
             "2020-06-01", "2020-12-31", "IRG-2008",
             30000, 40000, 0.40, 1000, 1500, "8/3", "20000/3", "5/3", "12500/3",
             "JO N° 33 du 04/06/2020 — Art. 104"),
            ("IRG-PER-2021", "IRG-PER-2021",
             "IRG — barème 2008 + exonération 30 000 + lissage 8/3 (plafond spé 42 500)",
             "2021-01-01", "2021-12-31", "IRG-2008",
             30000, 42500, 0.40, 1000, 1500, "8/3", "20000/3", "5/3", "12500/3",
             "Loi de finances 2021 — Art. 68"),
            // Q-01 (14/07/2026) : nouveau barème LF 2022 (6 tranches) + lissages
            // conservés — général 137/51 − 27925/8 (30–35 k), spécial 93/61 −
            // 81213/41 (30 000 < SI < 42 500, handicapés/retraités RG).
            ("IRG-PER-2022", "IRG-PER-2022",
             "IRG — barème 2022 (6 tranches) + exonération 30 000 + lissage 137/51 (plafond spé 42 500)",
             "2022-01-01", null, "IRG-2022",
             30000, 42500, 0.40, 1000, 1500, "137/51", "27925/8", "93/61", "81213/41",
             "Loi de finances 2022 — Art. 31 & 66"),
        };

        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var g in regles)
        {
            ct.ThrowIfCancellationRequested();
            var n = await c.ExecuteAsync("""
                INSERT INTO IRGReglesPeriode
                    (Id, Code, Libelle, DateDebut, DateFin, BaremeId,
                     ExonerationSeuil, AbattementTaux, AbattementMin, AbattementMax,
                     CoefGeneral, ConstGeneral, CoefSpecial, ConstSpecial, PlafondSpecial,
                     Source, Hash, CreatedAt)
                VALUES
                    ($id, $code, $l, $dd, $df, $b, $ex, $ta, $amin, $amax,
                     $cg, $cng, $cs, $cns, $ps, $src, $h, $at)
                ON CONFLICT(Id) DO NOTHING;
                """,
                new
                {
                    id = g.Id, code = g.Code, l = g.Libelle,
                    dd = g.DateDebut, df = g.DateFin ?? (object)DBNull.Value,
                    b = g.BaremeId, ex = g.Exon,
                    ta = g.TauxAbattement, amin = g.MinAbattement, amax = g.MaxAbattement,
                    cg = g.CoefG, cng = g.ConstG, cs = g.CoefS, cns = g.ConstS,
                    ps = g.PlafondSpe, src = g.Source,
                    h = $"sha256:{g.Id}",
                    at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                }, tx);
            inserted += n;
        }
        tx.Commit();
        r.Add("IRGReglesPeriode", regles.Length, inserted);
    }
}
