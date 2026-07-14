using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;

namespace PaieEducation.Tools.Seeding;

/// <summary>
/// Seeder du référentiel IRG : barème 2008 (4 tranches) + 4 règles de
/// période (avant 2020-06, 2020-06..12, 2021, 2022+). Les coefficients et
/// constantes sont stockés en <b>TEXT</b> sous forme de fractions
/// canoniques (« 8/3 », « 20000/3 », « 137/51 », « 27925/8 », « 93/61 »,
/// « 81213/41 ») — voir V007.
/// </summary>
/// <remarks>
/// <para>Sources :</para>
/// <list type="bullet">
///   <item><c>Reglementation/IRG_Algerie_2008_2026/CALCUL IRG ALGERIE.txt</c></item>
///   <item><c>Reglementation/IRG_Algerie_2008_2026/EXPLICATION BAREME IRG 2020 APPL (2).docx</c></item>
///   <item><c>Reglementation/IRG_Algerie_2008_2026/IRG_Algerie_2008_2026_PseudoCode.txt</c></item>
///   <item><c>docs/PLAN_ACTION.md</c> Q4, Q4b, Q5</item>
/// </list>
/// <para>Idempotent (<c>INSERT OR IGNORE</c>).</para>
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
    // Barème 2008 (en-tête)
    // -------------------------------------------------------------------------
    private static async Task InsertBaremeAsync(SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO BaremeIRG (Id, Code, Libelle, DateEffet, Source, Hash, CreatedAt)
            VALUES ($id, $c, $l, $de, $src, $h, $at)
            ON CONFLICT(Id) DO NOTHING;
            """;
        using var tx = c.BeginTransaction();
        var n = await c.ExecuteAsync(sql, new
        {
            id = "IRG-2008", c = "IRG-2008",
            l = "Barème progressif mensuel (loi de finances 2008)",
            de = "2007-01-01",
            src = "JO N° 82 du 31/12/2007 — Art. 104 code des impôts directs",
            h = "sha256:bareme-irg-2008",
            at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        }, tx);
        tx.Commit();
        r.Add("BaremeIRG", 1, n);
    }

    // -------------------------------------------------------------------------
    // Barème 2008 — 4 tranches (0/20/30/35 %)
    // -------------------------------------------------------------------------
    private static async Task InsertTranchesAsync(SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var tranches = new (string Id, int BorneInf, int? BorneSup, double Taux, int Ordre)[]
        {
            ("IRG-2008-T1", 0,      10000,   0.00, 1),
            ("IRG-2008-T2", 10001,  30000,   0.20, 2),
            ("IRG-2008-T3", 30001,  120000,  0.30, 3),
            ("IRG-2008-T4", 120001, null,    0.35, 4),
        };

        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var tr in tranches)
        {
            ct.ThrowIfCancellationRequested();
            var n = await c.ExecuteAsync("""
                INSERT INTO BaremeIRGTranches
                    (Id, BaremeId, BorneInf, BorneSup, Taux, Ordre, Source, Hash, CreatedAt)
                VALUES
                    ($id, $b, $bi, $bs, $taux, $o, $src, $h, $at)
                ON CONFLICT(Id) DO NOTHING;
                """,
                new
                {
                    id = tr.Id, b = "IRG-2008", bi = tr.BorneInf,
                    bs = tr.BorneSup ?? (object)DBNull.Value, taux = tr.Taux, o = tr.Ordre,
                    src = "JO 82/2007",
                    h = $"h-{tr.Id}",
                    at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                }, tx);
            inserted += n;
        }
        tx.Commit();
        r.Add("BaremeIRGTranches", tranches.Length, inserted);
    }

    // -------------------------------------------------------------------------
    // IRGReglesPeriode — 4 règles (avant 2020-06, 2020-06..12, 2021, 2022+)
    // -------------------------------------------------------------------------
    private static async Task InsertReglesPeriodeAsync(SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var regles = new (string Id, string Code, string Libelle, string DateDebut, string? DateFin,
            int Exon, int PlafondSpe, string CoefG, string ConstG, string CoefS, string ConstS, string Source)[]
        {
            ("IRG-PER-AV-2020-06", "IRG-PER-AV-2020-06",
             "IRG — barème 2008 (sans exonération, sans lissage)",
             "1000-01-01", "2020-05-31",
             0, 0, "1", "0", "1", "0",
             "Barème 2008 — application directe"),
            ("IRG-PER-2020-06", "IRG-PER-2020-06",
             "IRG — barème 2008 + exonération 30 000 + lissage 8/3 (plafond spé 40 000)",
             "2020-06-01", "2020-12-31",
             30000, 40000, "8/3", "20000/3", "5/3", "12500/3",
             "JO N° 33 du 04/06/2020 — Art. 104"),
            ("IRG-PER-2021", "IRG-PER-2021",
             "IRG — barème 2008 + exonération 30 000 + lissage 8/3 (plafond spé 42 500)",
             "2021-01-01", "2021-12-31",
             30000, 42500, "8/3", "20000/3", "5/3", "12500/3",
             "Loi de finances 2021 — Art. 68"),
            ("IRG-PER-2022", "IRG-PER-2022",
             "IRG — barème 2008 + exonération 30 000 + lissage 137/51 (plafond spé 42 500)",
             "2022-01-01", null,
             30000, 42500, "137/51", "27925/8", "93/61", "81213/41",
             "Loi de finances 2022 — Art. 66"),
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
                    ($id, $code, $l, $dd, $df, $b, $ex, 0.40, 1000, 1500,
                     $cg, $cng, $cs, $cns, $ps, $src, $h, $at)
                ON CONFLICT(Id) DO NOTHING;
                """,
                new
                {
                    id = g.Id, code = g.Code, l = g.Libelle,
                    dd = g.DateDebut, df = g.DateFin ?? (object)DBNull.Value,
                    b = "IRG-2008", ex = g.Exon,
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
