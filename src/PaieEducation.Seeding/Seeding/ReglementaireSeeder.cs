using System.Globalization;
using System.Linq;
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
/// <para>Deux sections restent en C# pour V1 : les groupes DNF
/// d'éligibilité ISSRP (6 groupes × ~92 grade IDs) et les 4 grades
/// hors catégorie (Q-C3 résolue le 16/07/2026). Le volume et le
/// couplage avec la logique d'éligibilité justifient un format de
/// fichier dédié (lot suivant). Une refactorisation de ces sections
/// est <b>explicitement reportée</b> dans <c>docs/audit/PLAN_IMPLEMENTATION.md</c>.</para>
/// </remarks>
public sealed class ReglementaireSeeder
{
    // ----- Constantes de la matrice ISSRP, remodelée en groupes DNF au grain
    // GRADE (J4F_TABLEAU_ISSRP_GRAIN_GRADE.md, validée jalons A/B, 15-16/07/2026).
    // Remplace l'ancienne matrice à plat par CORPS (acronymes placeholders,
    // incompatible avec l'évaluateur DNF : « CORPS = X ET CORPS = Y » n'est
    // jamais vrai). Source : J3G (185 grades, Cascade_Corps_Grades_30526.csv,
    // validation Q-08 par arrêté 6 primes contractuels + ISSRP_Regles_Metier.md
    // pour les inspecteurs génériques). 4 grades (n° 144/145/146/148, catégorie
    // « hors catégorie » HC-S1/HC-S2) restent hors seed — ⛔ Q-C3, abstention
    // (ADR-0009) : le schéma `Categorie INTEGER` ne peut pas les représenter.
    //
    // Avant 2025-01-01 : taux unique 15 % pour TOUS les corps EN classés
    // (RM-042), y compris les 7 grades « conditionnels » (la distinction par
    // origine statutaire n'existe qu'à partir de 2025, D.ex. 25-55).
    // À partir de 2025-01-01 : 3 taux, dont 2 groupes conditionnés par
    // ORIGINE_STATUTAIRE (Q-C1 : jamais de <>, abstention si INCONNU).
    //
    // Q-C3 résolue (16/07/2026) : les 4 grades « hors catégorie » (HC-S1/HC-S2,
    // n° 144/145/146/148) sont réintégrés — indices trouvés dans
    // Reglementation/Statuts particuliers/Liste_Grades_Fr.csv (lignes 88-90, 92),
    // vérifiés ligne à ligne contre J3G/J4F. Seed supplémentaire ciblé
    // (InsertGradesHorsCategorieAsync), CascadeRow/CsvCascadeParser non touchés
    // (décision utilisateur). Seed ISSRP désormais complet : 185/185 grades.

    private const string IssrpPeriodeHistorique = "2008-01-01";
    private const string IssrpPeriodeHistoriqueFin = "2024-12-31";
    private const string IssrpPeriodeActuelle = "2025-01-01";
    private const string IssrpSource2025 = "Art. 10 D.ex. 25-55 (21/01/2025)";
    private const string IssrpSourceHist = "Art. 9 bis D.ex. 11-373 (26/10/2011, effet rétroactif 01/01/2008)";
    private const string IssrpSourceQ08 = "Arrêté 6 primes enseignants contractuels ; D.ex. 10-78 art. 3/7/8/9bis ; D.ex. 08-70 art. 2/3/4";

    /// <summary>Grades du groupe ISSRP 45 % direct (2025+) — 50 grades : les 47
    /// initiaux (dont les 3 contractuels, Q-08) + 3 grades hors catégorie
    /// (IDLS-G144, IDLS-G145, IDLS-G148 — Q-C3 résolue) : enseignants, direction,
    /// censeurs, inspecteurs disciplines/administration.</summary>
    internal static readonly string[] Issrp45DirectGrades = new[]
    {
        "CDL-G014", "C-G015", "C-G016", "C-G017", "DDEP-G028", "DDEP-G029", "DDC-G030",
        "DDL-G031", "DDÈP-G032", "DDC-G033", "DDL-G034", "MDLP-G104", "PDLP-G105", "PDLP-G106",
        "PDLP-G107", "PDLP-G108", "PDLP-G109", "PDLP-G110", "PDLP-G111", "PDLP-G112", "PDLF-G113",
        "PDLM-G114", "PDLM-G115", "PDLM-G116", "PDLM-G117", "PDLM-G118", "PDLM-G119", "PDLM-G120",
        "PTDL-G121", "PTDL-G122", "PDLS-G123", "PDLS-G124", "PDLS-G125", "PDLS-G126", "PDLS-G127",
        "PDLS-G128", "PDLS-G129", "IDLP-G133", "IDLM-G135", "IDLN-G136", "IDLP-G137", "IDLP-G138",
        "IDLM-G140", "IDLM-G141", "PDLP-G130", "PDLM-G131", "PDLS-G132",
        "IDLS-G144", "IDLS-G145", "IDLS-G148",
    };

    /// <summary>7 grades conditionnels (Éducateurs spécialisés, Conseillers de
    /// l'Education) : 45 % si <c>ORIGINE_STATUTAIRE = ENSEIGNANT</c>, 30 % si
    /// <c>= AUTRE</c>, aucun des deux si <c>INCONNU</c> (Q-C1, abstention).</summary>
    internal static readonly string[] IssrpOrigineGrades = new[]
    {
        "SDL-G007", "SDL-G008", "SDL-G009", "SDL-G010", "CDL-G011", "CDL-G012", "CDL-G013",
    };

    /// <summary>Grades du groupe ISSRP 30 % direct (2025+) — 20 grades : les 19
    /// initiaux + 1 grade hors catégorie (IDLS-G146 — Q-C3 résolue) : éducateurs
    /// non issus du corps enseignant, orientation/guidance, alimentation scolaire.</summary>
    internal static readonly string[] Issrp30DirectGrades = new[]
    {
        "ADL-G001", "ADL-G002", "SDL-G003", "SDL-G004", "SDL-G005", "SDL-G006",
        "CDLSEP-G018", "CDLEDLGSEP-G019", "CDLEDLGSEP-G020", "CDLEDLGSEP-G021", "CDLEDLGSEP-G022",
        "CDLEDLGSEP-G023", "CEAS-G024", "CEAS-G025", "CEAS-G026", "CEAS-G027", "IDLEDLGSEP-G134",
        "IDLP-G139", "IDLM-G142", "IDLS-G146",
    };

    /// <summary>Grades du groupe ISSRP 15 % (intendance, laboratoire,
    /// inspecteurs gestion financière) — taux inchangé en 2025+.</summary>
    internal static readonly string[] Issrp15DirectGrades = new[]
    {
        "ADSE-G035", "ADSE-G036", "S-G037", "S-G038", "I-G039", "I-G040", "ATDL-G041",
        "ATDL-G042", "ATDL-G043", "ADL-G044", "ADL-G045", "ADL-G046", "ADL-G047", "IDLM-G143",
        "IDLS-G147",
    };

    /// <summary>Historique 2008-2024 : taux unique 15 % pour tous les corps EN
    /// classés (RM-042) — union des 4 groupes 2025+, conditionnels et
    /// contractuels inclus (aucune distinction d'origine avant D.ex. 25-55).</summary>
    internal static readonly string[] IssrpHistGrades =
        Issrp45DirectGrades.Concat(IssrpOrigineGrades).Concat(Issrp30DirectGrades).Concat(Issrp15DirectGrades).ToArray();

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

    /// <summary>Une condition atomique d'un groupe DNF : (critère, opérateur, valeur).</summary>
    private readonly record struct Condition(string CritereId, string Operateur, string Valeur);

    /// <summary>Un groupe DNF ISSRP : en-tête + conditions ETées (le OU se fait
    /// entre groupes portant le même <c>RubriqueId</c>, via l'évaluateur DNF).</summary>
    private readonly record struct GroupeIssrp(
        string GroupeId, string RubriqueId, string DateEffet, string? DateFin,
        string Source, Condition[] Conditions);

    // -------------------------------------------------------------------------
    // Q-C3 (résolue 16/07/2026) — 4 grades « hors catégorie » (HC-S1/HC-S2),
    // absents du CSV principal (Cascade_Corps_Grades_30526.csv : Categorie
    // non numérique, CascadeRow.Categorie=int rejette ces 4 lignes en amont —
    // CsvCascadeParser non touché, décision utilisateur). Seed supplémentaire
    // ciblé, indépendant de NomenclatureSeeder, sourcé sur
    // Reglementation/Statuts particuliers/Liste_Grades_Fr.csv (lignes 88-90,
    // 92) — indices vérifiés ligne à ligne contre J3G/J4F.
    //
    // GrilleIndiciaire : pas de ligne pour la période « avant 01/03/2022 »
    // (indice = 0 dans la source — ces subdivisions hors catégorie n'avaient
    // pas d'indice avant cette date ; IndiceMin > 0 interdit de toute façon
    // la valeur 0, cf. CHECK V003).
    //
    // NOTE Lot 1.3 : reste en C# pour V1. Sa migration vers le JSON est
    // explicitement reportée — voir commentaire de classe.
    // -------------------------------------------------------------------------
    private static async Task InsertGradesHorsCategorieAsync(
        SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        const string source = "Reglementation/Statuts particuliers/Liste_Grades_Fr.csv (lignes 88-90, 92)";
        var at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        var categories = new (string Id, int Niveau, string Libelle)[]
        {
            ("HC-S1", 18, "Hors catégorie - subdivision 1"),
            ("HC-S2", 19, "Hors catégorie - subdivision 2"),
        };

        var grilles = new (string CategorieId, string DateEffet, string? DateFin, int Indice, string Version)[]
        {
            ("HC-S1", "2022-03-01", "2022-12-31", 980, "2022-03"),
            ("HC-S1", "2023-01-01", "2023-12-31", 1055, "2023"),
            ("HC-S1", "2024-01-01", null, 1130, "2024"),
            ("HC-S2", "2022-03-01", "2022-12-31", 1040, "2022-03"),
            ("HC-S2", "2023-01-01", "2023-12-31", 1115, "2023"),
            ("HC-S2", "2024-01-01", null, 1190, "2024"),
        };

        var grades = new (string Id, string Libelle, int Ordre)[]
        {
            ("IDLS-G144", "Inspecteur de l'enseignement secondaire spécialité disciplines", 144),
            ("IDLS-G145", "Inspecteur de l'enseignement secondaire spécialité administration des lycées", 145),
            ("IDLS-G146", "Inspecteur de l'orientation et de la guidance scolaire et professionnelle aux lycées", 146),
            ("IDLS-G148", "Inspecteur de l'Education nationale", 148),
        };

        using var tx = c.BeginTransaction();
        var insFiliere = await c.ExecuteAsync("""
            INSERT INTO Filieres (Id, Libelle, Actif, CreatedAt, Source, Hash)
            VALUES ('INSPECTION', 'INSPECTION', 1, $at, $src, 'h-filiere-inspection')
            ON CONFLICT(Id) DO NOTHING;
            """, new { at, src = source }, tx);

        var insCorps = await c.ExecuteAsync("""
            INSERT INTO Corps (Id, Libelle, FiliereId, Actif, CreatedAt, Source, Hash)
            VALUES ('IDLS', $l, 'INSPECTION', 1, $at, $src, 'h-corps-idls')
            ON CONFLICT(Id) DO NOTHING;
            """, new { l = "Corps des Inspecteurs de l'enseignement secondaire", at, src = source }, tx);

        var insCat = 0;
        foreach (var cat in categories)
        {
            ct.ThrowIfCancellationRequested();
            insCat += await c.ExecuteAsync("""
                INSERT INTO Categories (Id, Niveau, Libelle, HorsCategorie, Actif, CreatedAt, Source, Hash)
                VALUES ($id, $niv, $l, 1, 1, $at, $src, $h)
                ON CONFLICT(Id) DO NOTHING;
                """,
                new { id = cat.Id, niv = cat.Niveau, l = cat.Libelle, at, src = source, h = $"h-cat-{cat.Id}" }, tx);
        }

        var insGrille = 0;
        foreach (var g in grilles)
        {
            ct.ThrowIfCancellationRequested();
            var id = $"GI-{g.CategorieId}-{g.DateEffet}";
            insGrille += await c.ExecuteAsync("""
                INSERT INTO GrilleIndiciaire (Id, CategorieId, DateEffet, DateFin, IndiceMin, Version, Source, Hash, CreatedAt)
                VALUES ($id, $cid, $de, $df, $i, $ver, $src, $h, $at)
                ON CONFLICT(Id) DO NOTHING;
                """,
                new
                {
                    id, cid = g.CategorieId, de = g.DateEffet, df = g.DateFin ?? (object)DBNull.Value,
                    i = g.Indice, ver = g.Version, src = source, h = $"h-grille-{id}", at
                }, tx);
        }

        var insGrades = 0;
        foreach (var g in grades)
        {
            ct.ThrowIfCancellationRequested();
            insGrades += await c.ExecuteAsync("""
                INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, Actif, CreatedAt, Source, Hash)
                VALUES ($id, $l, 'IDLS', $o, 1, $at, $src, $h)
                ON CONFLICT(Id) DO NOTHING;
                """,
                new { id = g.Id, l = g.Libelle, o = g.Ordre, at, src = source, h = $"h-grade-{g.Id}" }, tx);
        }

        tx.Commit();
        r.Add("Filieres", 1, insFiliere);
        r.Add("Corps", 1, insCorps);
        r.Add("Categories", categories.Length, insCat);
        r.Add("GrilleIndiciaire", grilles.Length, insGrille);
        r.Add("Grades", grades.Length, insGrades);
    }

    // -------------------------------------------------------------------------
    // GroupesEligibilite + ReglesEligibilite — ISSRP en groupes DNF (grain
    // GRADE). Remplace la matrice à plat par CORPS (J4F, validée jalons A/B).
    //
    // NOTE Lot 1.3 : reste en C# pour V1. Sa migration vers le JSON est
    // explicitement reportée — voir commentaire de classe. Le couplage
    // avec les arrays de grades (~92 IDs) et la structure DNF multi-conditions
    // demandent un format de fichier dédié.
    // -------------------------------------------------------------------------
    private static async Task InsertReglesEligibiliteAsync(
        SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        static string InListe(IReadOnlyCollection<string> grades) => string.Join(",", grades);

        var groupes = new[]
        {
            // 2025+ : 45 % direct (grades non conditionnels + 3 contractuels, Q-08).
            new GroupeIssrp("GE-ISSRP45-DIRECT", "ISSRP_45", IssrpPeriodeActuelle, null,
                IssrpSource2025, new[] { new Condition("GRADE", "IN", InListe(Issrp45DirectGrades)) }),

            // 2025+ : 45 % si origine enseignante (groupe conditionnel, OU avec DIRECT).
            new GroupeIssrp("GE-ISSRP45-ORIGINE", "ISSRP_45", IssrpPeriodeActuelle, null,
                IssrpSource2025, new[]
                {
                    new Condition("GRADE", "IN", InListe(IssrpOrigineGrades)),
                    new Condition("ORIGINE_STATUTAIRE", "=", "ENSEIGNANT"),
                }),

            // 2025+ : 30 % direct.
            new GroupeIssrp("GE-ISSRP30-DIRECT", "ISSRP_30", IssrpPeriodeActuelle, null,
                IssrpSource2025, new[] { new Condition("GRADE", "IN", InListe(Issrp30DirectGrades)) }),

            // 2025+ : 30 % si origine NON enseignante (Q-C1 : jamais `<>` — abstention
            // si ORIGINE_STATUTAIRE = INCONNU, aucun taux n'est déduit).
            new GroupeIssrp("GE-ISSRP30-ORIGINE", "ISSRP_30", IssrpPeriodeActuelle, null,
                IssrpSource2025, new[]
                {
                    new Condition("GRADE", "IN", InListe(IssrpOrigineGrades)),
                    new Condition("ORIGINE_STATUTAIRE", "=", "AUTRE"),
                }),

            // 2025+ : 15 % (intendance, laboratoire, gestion financière) — inchangé.
            new GroupeIssrp("GE-ISSRP15-DIRECT", "ISSRP_15", IssrpPeriodeActuelle, null,
                IssrpSource2025, new[] { new Condition("GRADE", "IN", InListe(Issrp15DirectGrades)) }),

            // 2008-2024 : taux unique 15 % pour tous les corps EN classés (RM-042),
            // conditionnels et contractuels inclus (pas de distinction d'origine
            // avant D.ex. 25-55).
            new GroupeIssrp("GE-ISSRP15-HIST", "ISSRP_15", IssrpPeriodeHistorique, IssrpPeriodeHistoriqueFin,
                IssrpSourceHist, new[] { new Condition("GRADE", "IN", InListe(IssrpHistGrades)) }),
        };

        var insertedGroupes = 0;
        var insertedConditions = 0;
        var totalConditions = 0;
        using var tx = c.BeginTransaction();
        var at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        foreach (var g in groupes)
        {
            ct.ThrowIfCancellationRequested();

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
                    h = $"h-groupe-{g.GroupeId}", at
                }, tx);
            insertedGroupes += nGroupe;

            for (var i = 0; i < g.Conditions.Length; i++)
            {
                var cond = g.Conditions[i];
                var condId = $"RE-{g.GroupeId}-{i}";
                totalConditions++;
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
                        op = cond.Operateur, v = cond.Valeur, de = g.DateEffet,
                        df = g.DateFin ?? (object)DBNull.Value, src = g.Source,
                        h = $"h-{condId}", at
                    }, tx);
                insertedConditions += n;
            }
        }

        tx.Commit();
        r.Add("GroupesEligibilite", groupes.Length, insertedGroupes);
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
