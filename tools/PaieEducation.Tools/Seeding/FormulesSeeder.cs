using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;

namespace PaieEducation.Tools.Seeding;

/// <summary>
/// Seed des <b>formules de calcul</b> du pilote enseignant (J4.c) —
/// <c>RubriqueFormules</c>. Les expressions sont stockées en base (texte lu par
/// le FormulaEngine), jamais codées en dur dans le moteur (ADR-0005, critère
/// « 0 règle réglementaire dans le code »).
/// </summary>
/// <remarks>
/// Ajoute aussi la rubrique <c>TRAITEMENT</c> (traitement mensuel de base =
/// ligne principale du bulletin) que le référentiel réglementaire ne portait
/// pas encore. Idempotent (<c>ON CONFLICT DO NOTHING</c>). Sources : catalogue
/// des formules J3C §1-2.
/// </remarks>
public sealed class FormulesSeeder
{
    private const string DateEffet = "2008-01-01";

    public async Task<SeedReport> SeedAsync(SqliteConnection conn, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(conn);
        if (conn.State != System.Data.ConnectionState.Open)
            throw new InvalidOperationException("La connexion doit être ouverte.");

        var report = new SeedReport(0);
        await InsertTraitementRubriqueAsync(conn, report, ct).ConfigureAwait(false);
        await InsertFormulesAsync(conn, report, ct).ConfigureAwait(false);
        return report;
    }

    // TRAITEMENT — traitement mensuel de base (TRT), ligne principale du bulletin.
    private static async Task InsertTraitementRubriqueAsync(SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        // Non affectable manuellement (V-2, J4E § 5) : base structurelle du
        // bulletin — la supprimer priverait l'agent de tout salaire.
        var n = await c.ExecuteAsync("""
            INSERT INTO Rubriques
                (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul,
                 EstImposable, EstCotisable, Description, Actif, CreatedAt, Source, Hash,
                 EstAffectableManuellement, OccurrencesMultiples)
            VALUES
                ('TRAITEMENT', 'Traitement mensuel de base', 'GAIN', 'INDICE_ECHELON',
                 'MENSUELLE', 100, 1, 1,
                 'Traitement principal (INDICE_MIN + INDICE_ECH) × valeur du point',
                 1, $at, 'J4.c — pilote', 'h-rubrique-TRAITEMENT', 0, 0)
            ON CONFLICT(Id) DO NOTHING;
            """,
            new { at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) });
        r.Add("Rubriques (TRAITEMENT)", 1, n);
    }

    private static async Task InsertFormulesAsync(SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        // (RubriqueId, Expression) — catalogue J3C. Les taux littéraux vivent dans
        // l'expression EN BASE (pas dans le code) ; ils sont éditables via le
        // Workbench (Phase 6) sans recompilation.
        var formules = new (string RubriqueId, string Expression, string Source)[]
        {
            ("TRAITEMENT", "(INDICE_MIN + INDICE_ECH) * VPI", "J3C §1 — TRT = (indice min + indice échelon) × VPI"),
            ("EXP_PEDAG",  "TBASE * 0.04 * ECH",              "J3C §2 — 4 % × TBASE × n° échelon"),
            ("PAPP",       "TRT * valeurSource(PAPP)",        "J3C §2 — TRT × taux de notation (0–40 %)"),
            ("QUALIF",     "TRT * bareme(QUALIF, CATEGORIE)", "J3C §2 — TRT × taux par tranche de catégorie (40 %/45 %)"),
            ("DOC_PEDAG",  "bareme(DOC_PEDAG, CATEGORIE)",    "J3C §2 — forfait par tranche de catégorie"),
            ("ISSRP_45",   "TRT * 0.45",                      "J3C §2 — ISSRP 45 % (2025+)"),
        };

        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var f in formules)
        {
            ct.ThrowIfCancellationRequested();
            var n = await c.ExecuteAsync("""
                INSERT INTO RubriqueFormules
                    (Id, RubriqueId, DateEffet, DateFin, Expression, Ordre, Source, Hash, CreatedAt)
                VALUES
                    ($id, $r, $de, NULL, $e, 0, $src, $h, $at)
                ON CONFLICT(Id) DO NOTHING;
                """,
                new
                {
                    id = $"RF-{f.RubriqueId}-{DateEffet}",
                    r = f.RubriqueId, de = DateEffet, e = f.Expression, src = f.Source,
                    h = $"h-formule-{f.RubriqueId}",
                    at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                }, tx);
            inserted += n;
        }
        tx.Commit();
        r.Add("RubriqueFormules", formules.Length, inserted);
    }
}
