using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;

namespace PaieEducation.Seeding;

/// <summary>
/// Seeder du **référentiel réglementaire** : rubriques, règles
/// d'éligibilité (matrice corps→ISSRP), cotisations, paramètres système.
/// </summary>
/// <remarks>
/// <para>Les données seedées sont issues de :</para>
/// <list type="bullet">
///   <item><c>Reglementation/IRG_Algerie_2008_2026/ISSRP_Corrige_26526.txt</c>
///         (matrice ISSRP 45/30/15, taux historique 2008–2024 à 15 %)</item>
///   <item><c>docs/PLAN_ACTION.md</c> section A (Q3b : SS 9 %, Mutuelle,
///         Œuvres sociales ; Q9b : ARRONDI_MODE par défaut)</item>
///   <item><c>Reglementation/IRG_Algerie_2008_2026/CALCUL IRG ALGERIE.txt</c>
///         (cadre algorithmique de l'IRG — les barèmes et lissages sont
///         seedés séparément via la CLI <c>seed irg</c>)</item>
/// </list>
/// <para>Idempotent (<c>INSERT OR IGNORE</c>). Ré-exécuté plusieurs fois,
/// il ne duplique pas les lignes.</para>
/// <para><b>Lot 1.3 finalisation</b> : les 4 sections « plates »
/// (rubriques, barèmes, cotisations, paramètres) sont lues depuis
/// <c>Donnees/Reglementaire/referentiel_reglementaire_v1.json</c>
/// (cf. <see cref="ReglementaireJsonDataReader"/>), même pattern que
/// les barèmes IRG (Lot 1.3α) et les formules (Lot 1.3 final).
/// Hash SHA-256 sur chaque ligne → détection de drift.</para>
/// <para><b>Chantier P2 (19/07/2026)</b> : les groupes DNF d'éligibilité
/// ISSRP (6 groupes × ~92 grade IDs) et les 4 grades hors catégorie
/// (Q-C3 résolue le 16/07/2026), reportés lors du Lot 1.3, sont désormais
/// lus depuis <c>Donnees/Reglementaire/groupes_dnf_issrp_v1.json</c> (cf.
/// <see cref="GroupesDnfIssrpJsonDataReader"/>) — plus aucune valeur
/// réglementaire codée en dur dans cette classe.</para>
/// </remarks>
public sealed class ReglementaireSeeder
{
    /// <summary>
    /// Insère l'ensemble du référentiel réglementaire. La base doit être
    /// migrée. Idempotent.
    /// </summary>
    public async Task<SeedReport> SeedAsync(SqliteConnection conn, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(conn);
        if (conn.State != System.Data.ConnectionState.Open)
            throw new InvalidOperationException("La connexion doit être ouverte.");

        var report = new SeedReport(0);

        // Sections « plates » : depuis le JSON embarqué (Lot 1.3 finalisation).
        await InsertRubriquesAsync(conn, report, ct).ConfigureAwait(false);
        await InsertGradesHorsCategorieAsync(conn, report, ct).ConfigureAwait(false);
        await InsertReglesEligibiliteAsync(conn, report, ct).ConfigureAwait(false);
        await InsertRubriqueBaremesAsync(conn, report, ct).ConfigureAwait(false);
        await InsertCotisationsAsync(conn, report, ct).ConfigureAwait(false);
        await InsertParametresAsync(conn, report, ct).ConfigureAwait(false);

        return report;
    }

    // -------------------------------------------------------------------------
    // Rubriques (10 au total) — depuis le JSON
    // -------------------------------------------------------------------------
    private static async Task InsertRubriquesAsync(
        SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var data = ReglementaireJsonDataReader.Load();
        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var rub in data.Rubriques)
        {
            ct.ThrowIfCancellationRequested();
            var hash = ReglementaireJsonDataReader.HashLigne(new
            {
                rub.Id, rub.Libelle, rub.Nature, rub.BaseCalcul, rub.Periodicite,
                rub.PeriodiciteVersement, rub.OrdreCalcul, rub.EstImposable, rub.EstCotisable,
                rub.Description, rub.EstAffectableManuellement, rub.OccurrencesMultiples,
            });
            var sql = """
                INSERT INTO Rubriques
                    (Id, Libelle, Nature, BaseCalcul, Periodicite, PeriodiciteVersement,
                     OrdreCalcul, EstImposable, EstCotisable, Description, Actif,
                     CreatedAt, Source, Hash, EstAffectableManuellement, OccurrencesMultiples)
                VALUES
                    ($id, $l, $n, $b, $p, $pv, $o, $ei, $ec, $d, 1,
                     $at, 'J2.c/J3 — seed réglementaire', $h, $eam, $om)
                ON CONFLICT(Id) DO NOTHING;
                """;
            var n = await c.ExecuteAsync(sql, new
            {
                id = rub.Id, l = rub.Libelle, n = rub.Nature, b = rub.BaseCalcul,
                p = rub.Periodicite, pv = rub.PeriodiciteVersement ?? (object)DBNull.Value,
                o = rub.OrdreCalcul, ei = rub.EstImposable ? 1 : 0,
                ec = rub.EstCotisable ? 1 : 0, d = rub.Description,
                at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                h = hash,
                eam = rub.EstAffectableManuellement ? 1 : 0,
                om = rub.OccurrencesMultiples ? 1 : 0
            }, tx);
            inserted += n;
        }
        tx.Commit();
        r.Add("Rubriques", data.Rubriques.Count, inserted);
    }

    // -------------------------------------------------------------------------
    // Q-C3 (résolue 16/07/2026) — 4 grades « hors catégorie » (HC-S1/HC-S2),
    // absents du CSV principal (Cascade_Corps_Grades_30526.csv : Categorie
    // non numérique, CascadeRow.Categorie=int rejette ces 4 lignes en amont —
    // CsvCascadeParser non touché, décision utilisateur). Seed supplémentaire
    // ciblé, indépendant de NomenclatureSeeder.
    //
    // GrilleIndiciaire : pas de ligne pour la période « avant 01/03/2022 »
    // (indice = 0 dans la source — ces subdivisions hors catégorie n'avaient
    // pas d'indice avant cette date ; IndiceMin > 0 interdit de toute façon
    // la valeur 0, cf. CHECK V003).
    //
    // Chantier P2 : lu depuis groupes_dnf_issrp_v1.json (GradesHorsCategorie),
    // hash SHA-256 canonique par ligne (même mécanisme que les 4 autres
    // sections, ex-hash factices "h-cat-{id}" remplacés).
    // -------------------------------------------------------------------------
    private static async Task InsertGradesHorsCategorieAsync(
        SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var hc = GroupesDnfIssrpJsonDataReader.Load().GradesHorsCategorie;
        var at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        using var tx = c.BeginTransaction();
        var hFiliere = ReglementaireJsonDataReader.HashLigne(hc.Filiere);
        var insFiliere = await c.ExecuteAsync("""
            INSERT INTO Filieres (Id, Libelle, Actif, CreatedAt, Source, Hash)
            VALUES ($id, $l, 1, $at, $src, $h)
            ON CONFLICT(Id) DO NOTHING;
            """, new { id = hc.Filiere.Id, l = hc.Filiere.Libelle, at, src = hc.Source, h = hFiliere }, tx);

        var hCorps = ReglementaireJsonDataReader.HashLigne(hc.Corps);
        var insCorps = await c.ExecuteAsync("""
            INSERT INTO Corps (Id, Libelle, FiliereId, Actif, CreatedAt, Source, Hash)
            VALUES ($id, $l, $fid, 1, $at, $src, $h)
            ON CONFLICT(Id) DO NOTHING;
            """, new { id = hc.Corps.Id, l = hc.Corps.Libelle, fid = hc.Corps.FiliereId, at, src = hc.Source, h = hCorps }, tx);

        var insCat = 0;
        foreach (var cat in hc.Categories)
        {
            ct.ThrowIfCancellationRequested();
            var h = ReglementaireJsonDataReader.HashLigne(cat);
            insCat += await c.ExecuteAsync("""
                INSERT INTO Categories (Id, Niveau, Libelle, HorsCategorie, Actif, CreatedAt, Source, Hash)
                VALUES ($id, $niv, $l, 1, 1, $at, $src, $h)
                ON CONFLICT(Id) DO NOTHING;
                """,
                new { id = cat.Id, niv = cat.Niveau, l = cat.Libelle, at, src = hc.Source, h }, tx);
        }

        var insGrille = 0;
        foreach (var g in hc.GrilleIndiciaire)
        {
            ct.ThrowIfCancellationRequested();
            var id = $"GI-{g.CategorieId}-{g.DateEffet}";
            var h = ReglementaireJsonDataReader.HashLigne(g);
            insGrille += await c.ExecuteAsync("""
                INSERT INTO GrilleIndiciaire (Id, CategorieId, DateEffet, DateFin, IndiceMin, Version, Source, Hash, CreatedAt)
                VALUES ($id, $cid, $de, $df, $i, $ver, $src, $h, $at)
                ON CONFLICT(Id) DO NOTHING;
                """,
                new
                {
                    id, cid = g.CategorieId, de = g.DateEffet, df = g.DateFin ?? (object)DBNull.Value,
                    i = g.Indice, ver = g.Version, src = hc.Source, h, at
                }, tx);
        }

        var insGrades = 0;
        foreach (var g in hc.Grades)
        {
            ct.ThrowIfCancellationRequested();
            var h = ReglementaireJsonDataReader.HashLigne(g);
            insGrades += await c.ExecuteAsync("""
                INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, Actif, CreatedAt, Source, Hash)
                VALUES ($id, $l, $cid, $o, 1, $at, $src, $h)
                ON CONFLICT(Id) DO NOTHING;
                """,
                new { id = g.Id, l = g.Libelle, cid = hc.Corps.Id, o = g.Ordre, at, src = hc.Source, h }, tx);
        }

        tx.Commit();
        r.Add("Filieres", 1, insFiliere);
        r.Add("Corps", 1, insCorps);
        r.Add("Categories", hc.Categories.Count, insCat);
        r.Add("GrilleIndiciaire", hc.GrilleIndiciaire.Count, insGrille);
        r.Add("Grades", hc.Grades.Count, insGrades);
    }

    // -------------------------------------------------------------------------
    // GroupesEligibilite + ReglesEligibilite — ISSRP en groupes DNF (grain
    // GRADE). Remplace la matrice à plat par CORPS (J4F, validée jalons A/B).
    //
    // Chantier P2 : lu depuis groupes_dnf_issrp_v1.json (Groupes + Grades
    // nommés, résolus via GroupesDnfIssrpJsonDataReader.ResoudreGrades),
    // hash SHA-256 canonique par ligne (ex-hash factices "h-groupe-{id}"
    // remplacés).
    // -------------------------------------------------------------------------
    private static async Task InsertReglesEligibiliteAsync(
        SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var data = GroupesDnfIssrpJsonDataReader.Load();

        var insertedGroupes = 0;
        var insertedConditions = 0;
        var totalConditions = 0;
        using var tx = c.BeginTransaction();
        var at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        foreach (var g in data.Groupes)
        {
            ct.ThrowIfCancellationRequested();

            var hGroupe = ReglementaireJsonDataReader.HashLigne(g);
            var nGroupe = await c.ExecuteAsync("""
                INSERT INTO GroupesEligibilite
                    (Id, RubriqueId, Severite, MessageId, Priorite, DateEffet, DateFin,
                     Source, Hash, CreatedAt, CreatedBy)
                VALUES
                    ($id, $r, 'OBLIGATOIRE_REGLEMENTAIRE', NULL, 100, $de, $df,
                     $src, $h, $at, 'system')
                ON CONFLICT(Id) DO NOTHING;
                """,
                new
                {
                    id = g.GroupeId, r = g.RubriqueId, de = g.DateEffet,
                    df = g.DateFin ?? (object)DBNull.Value, src = g.Source,
                    h = hGroupe, at
                }, tx);
            insertedGroupes += nGroupe;

            for (var i = 0; i < g.Conditions.Count; i++)
            {
                var cond = g.Conditions[i];
                // GradesRefs (condition GRADE) : résolu en union ordonnée puis
                // joint en liste IN ; sinon la valeur littérale du JSON (ex.
                // ORIGINE_STATUTAIRE = ENSEIGNANT).
                var valeur = cond.GradesRefs is { } refs
                    ? string.Join(",", GroupesDnfIssrpJsonDataReader.ResoudreGrades(data, refs))
                    : cond.Valeur ?? throw new InvalidOperationException(
                        $"Condition {g.GroupeId}[{i}] : ni GradesRefs ni Valeur renseignés.");

                var condId = $"RE-{g.GroupeId}-{i}";
                totalConditions++;
                var h = ReglementaireJsonDataReader.HashLigne(new { g.GroupeId, cond.CritereId, cond.Operateur, valeur });
                var n = await c.ExecuteAsync("""
                    INSERT INTO ReglesEligibilite
                        (Id, RubriqueId, CritereId, GroupeId, Operateur, Valeur,
                         DateEffet, DateFin, Source, Hash, CreatedAt)
                    VALUES
                        ($id, $r, $crit, $grp, $op, $v, $de, $df, $src, $h, $at)
                    ON CONFLICT(Id) DO NOTHING;
                    """,
                    new
                    {
                        id = condId, r = g.RubriqueId, crit = cond.CritereId, grp = g.GroupeId,
                        op = cond.Operateur, v = valeur, de = g.DateEffet,
                        df = g.DateFin ?? (object)DBNull.Value, src = g.Source,
                        h, at
                    }, tx);
                insertedConditions += n;
            }
        }

        tx.Commit();
        r.Add("GroupesEligibilite", data.Groupes.Count, insertedGroupes);
        r.Add("ReglesEligibilite", totalConditions, insertedConditions);
    }

    // -------------------------------------------------------------------------
    // RubriqueBaremes — depuis le JSON (QUALIF + DOC_PEDAG)
    // -------------------------------------------------------------------------
    private static async Task InsertRubriqueBaremesAsync(
        SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var data = ReglementaireJsonDataReader.Load();
        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var b in data.Baremes)
        {
            ct.ThrowIfCancellationRequested();
            var hash = ReglementaireJsonDataReader.HashLigne(new
            {
                b.Id, b.RubriqueId, b.Dimension, b.BorneInf, b.BorneSup,
                b.TypeValeur, b.Valeur, b.DateEffet, b.Source,
            });
            var n = await c.ExecuteAsync("""
                INSERT INTO RubriqueBaremes
                    (Id, RubriqueId, Dimension, BorneInf, BorneSup, TypeValeur, Valeur,
                     DateEffet, Source, Hash, CreatedAt)
                VALUES
                    ($id, $r, $dim, $bi, $bs, $tv, $v, $de, $src, $h, $at)
                ON CONFLICT(Id) DO NOTHING;
                """,
                new
            {
                id = b.Id, r = b.RubriqueId, dim = b.Dimension, bi = b.BorneInf,
                bs = b.BorneSup ?? (object)DBNull.Value, tv = b.TypeValeur, v = b.Valeur,
                de = b.DateEffet, src = b.Source, h = hash,
                at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            }, tx);
            inserted += n;
        }
        tx.Commit();
        r.Add("RubriqueBaremes", data.Baremes.Count, inserted);
    }

    // -------------------------------------------------------------------------
    // Cotisations — depuis le JSON (Q3b)
    // -------------------------------------------------------------------------
    private static async Task InsertCotisationsAsync(
        SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var data = ReglementaireJsonDataReader.Load();
        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var cot in data.Cotisations)
        {
            ct.ThrowIfCancellationRequested();
            var hash = ReglementaireJsonDataReader.HashLigne(new
            {
                cot.Id, cot.Code, cot.Libelle, cot.Type, cot.Taux, cot.AssietteRef,
                cot.EstRetenue, cot.DateEffet, cot.Source,
            });
            var sql = """
                INSERT INTO Cotisations
                    (Id, Code, Libelle, TypeCotisation, Taux, AssietteRef, EstRetenue,
                     DateEffet, Source, Hash, CreatedAt)
                VALUES
                    ($id, $c, $l, $t, $tx, $ar, $er, $de, $src, $h, $at)
                ON CONFLICT(Id) DO NOTHING;
                """;
            var n = await c.ExecuteAsync(sql, new
            {
                id = cot.Id, c = cot.Code, l = cot.Libelle, t = cot.Type,
                tx = cot.Taux, ar = cot.AssietteRef, er = cot.EstRetenue ? 1 : 0,
                de = cot.DateEffet, src = cot.Source,
                h = hash,
                at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            }, tx);
            inserted += n;
        }
        tx.Commit();
        r.Add("Cotisations", data.Cotisations.Count, inserted);
    }

    // -------------------------------------------------------------------------
    // Paramètres système — depuis le JSON (Q9b : ARRONDI_MODE, etc.)
    // -------------------------------------------------------------------------
    private static async Task InsertParametresAsync(
        SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var data = ReglementaireJsonDataReader.Load();
        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var p in data.Parametres)
        {
            ct.ThrowIfCancellationRequested();
            var hash = ReglementaireJsonDataReader.HashLigne(new
            {
                p.Id, p.Cle, p.Valeur, p.Type, p.Description, p.DateEffet,
            });
            var sql = """
                INSERT INTO Parametres
                    (Id, Cle, Valeur, Type, Description, DateEffet, Source, Hash, CreatedAt)
                VALUES
                    ($id, $c, $v, $t, $d, $de, 'J2.c — paramètres système', $h, $at)
                ON CONFLICT(Id) DO NOTHING;
                """;
            var n = await c.ExecuteAsync(sql, new
            {
                id = p.Id, c = p.Cle, v = p.Valeur, t = p.Type, d = p.Description, de = p.DateEffet,
                h = hash,
                at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            }, tx);
            inserted += n;
        }
        tx.Commit();
        r.Add("Parametres", data.Parametres.Count, inserted);
    }
}
