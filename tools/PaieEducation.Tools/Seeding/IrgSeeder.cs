using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;

namespace PaieEducation.Tools.Seeding;

/// <summary>
/// Seeder du référentiel IRG : barème 2008 (4 tranches) + barème 2022
/// (6 tranches, LF 2022 — refonte totale) + 4 règles de période
/// (avant 2020-06, 2020-06..12, 2021, 2022+). Les coefficients et
/// constantes sont stockés en <b>TEXT</b> sous forme de fractions
/// canoniques (« 8/3 », « 20000/3 », « 137/51 », « 27925/8 », « 93/61 »,
/// « 81213/41 ») — voir V007.
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
    // Barèmes (en-têtes) : 2008 (LF 2008) et 2022 (LF 2022, refonte totale)
    // -------------------------------------------------------------------------
    private static async Task InsertBaremeAsync(SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var baremes = new (string Id, string Libelle, string DateEffet, string? DateFin, string Source)[]
        {
            ("IRG-2008",
             "Barème progressif mensuel (loi de finances 2008)",
             "2007-01-01", "2021-12-31",
             "JO N° 82 du 31/12/2007 — Art. 104 code des impôts directs"),
            // Q-01 (14/07/2026, révise Q4b) : la LF 2022 refond le barème
            // (6 tranches, seuil mensuel 20 000 DA). Pseudo-code étape 1 :
            // « à partir de 2022-01-01 ⇒ nouveau barème ».
            ("IRG-2022",
             "Barème progressif mensuel (loi de finances 2022 — 6 tranches)",
             "2022-01-01", null,
             "LF 2022 — Art. 31 → Art. 104 CIDTA révisé"),
        };

        const string sql = """
            INSERT INTO BaremeIRG (Id, Code, Libelle, DateEffet, DateFin, Source, Hash, CreatedAt)
            VALUES ($id, $c, $l, $de, $df, $src, $h, $at)
            ON CONFLICT(Id) DO NOTHING;
            """;
        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var b in baremes)
        {
            ct.ThrowIfCancellationRequested();
            var n = await c.ExecuteAsync(sql, new
            {
                id = b.Id, c = b.Id, l = b.Libelle,
                de = b.DateEffet, df = b.DateFin ?? (object)DBNull.Value,
                src = b.Source,
                h = $"sha256:bareme-{b.Id}",
                at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            }, tx);
            inserted += n;
        }
        tx.Commit();
        r.Add("BaremeIRG", baremes.Length, inserted);
    }

    // -------------------------------------------------------------------------
    // Tranches : barème 2008 (0/20/30/35 %) + barème 2022 (0/23/27/30/33/35 %)
    // -------------------------------------------------------------------------
    private static async Task InsertTranchesAsync(SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var tranches = new (string Id, string BaremeId, int BorneInf, int? BorneSup, double Taux, int Ordre, string Source)[]
        {
            // Barème 2008 — mensuel, 4 tranches.
            ("IRG-2008-T1", "IRG-2008", 0,      10000,   0.00, 1, "JO 82/2007"),
            ("IRG-2008-T2", "IRG-2008", 10001,  30000,   0.20, 2, "JO 82/2007"),
            ("IRG-2008-T3", "IRG-2008", 30001,  120000,  0.30, 3, "JO 82/2007"),
            ("IRG-2008-T4", "IRG-2008", 120001, null,    0.35, 4, "JO 82/2007"),
            // Barème 2022 — mensuel, 6 tranches (LF 2022, Art. 104 CIDTA révisé).
            ("IRG-2022-T1", "IRG-2022", 0,      20000,   0.00, 1, "LF 2022 — Art. 31"),
            ("IRG-2022-T2", "IRG-2022", 20001,  40000,   0.23, 2, "LF 2022 — Art. 31"),
            ("IRG-2022-T3", "IRG-2022", 40001,  80000,   0.27, 3, "LF 2022 — Art. 31"),
            ("IRG-2022-T4", "IRG-2022", 80001,  160000,  0.30, 4, "LF 2022 — Art. 31"),
            ("IRG-2022-T5", "IRG-2022", 160001, 320000,  0.33, 5, "LF 2022 — Art. 31"),
            ("IRG-2022-T6", "IRG-2022", 320001, null,    0.35, 6, "LF 2022 — Art. 31"),
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
                    id = tr.Id, b = tr.BaremeId, bi = tr.BorneInf,
                    bs = tr.BorneSup ?? (object)DBNull.Value, taux = tr.Taux, o = tr.Ordre,
                    src = tr.Source,
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
            string BaremeId, int Exon, int PlafondSpe, string CoefG, string ConstG, string CoefS, string ConstS, string Source)[]
        {
            ("IRG-PER-AV-2020-06", "IRG-PER-AV-2020-06",
             "IRG — barème 2008 (sans exonération, sans lissage)",
             "1000-01-01", "2020-05-31", "IRG-2008",
             0, 0, "1", "0", "1", "0",
             "Barème 2008 — application directe"),
            ("IRG-PER-2020-06", "IRG-PER-2020-06",
             "IRG — barème 2008 + exonération 30 000 + lissage 8/3 (plafond spé 40 000)",
             "2020-06-01", "2020-12-31", "IRG-2008",
             30000, 40000, "8/3", "20000/3", "5/3", "12500/3",
             "JO N° 33 du 04/06/2020 — Art. 104"),
            ("IRG-PER-2021", "IRG-PER-2021",
             "IRG — barème 2008 + exonération 30 000 + lissage 8/3 (plafond spé 42 500)",
             "2021-01-01", "2021-12-31", "IRG-2008",
             30000, 42500, "8/3", "20000/3", "5/3", "12500/3",
             "Loi de finances 2021 — Art. 68"),
            // Q-01 (14/07/2026) : nouveau barème LF 2022 (6 tranches) + lissages
            // conservés — général 137/51 − 27925/8 (30–35 k), spécial 93/61 −
            // 81213/41 (30 000 < SI < 42 500, handicapés/retraités RG).
            ("IRG-PER-2022", "IRG-PER-2022",
             "IRG — barème 2022 (6 tranches) + exonération 30 000 + lissage 137/51 (plafond spé 42 500)",
             "2022-01-01", null, "IRG-2022",
             30000, 42500, "137/51", "27925/8", "93/61", "81213/41",
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
                    ($id, $code, $l, $dd, $df, $b, $ex, 0.40, 1000, 1500,
                     $cg, $cng, $cs, $cns, $ps, $src, $h, $at)
                ON CONFLICT(Id) DO NOTHING;
                """,
                new
                {
                    id = g.Id, code = g.Code, l = g.Libelle,
                    dd = g.DateDebut, df = g.DateFin ?? (object)DBNull.Value,
                    b = g.BaremeId, ex = g.Exon,
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
