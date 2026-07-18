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
/// il ne duplique pas les lignes. Le seeder ne fait pas de migration : la
/// base doit être déjà migrée (V001-V007).</para>
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

        await InsertRubriquesAsync(conn, report, ct).ConfigureAwait(false);
        await InsertGradesHorsCategorieAsync(conn, report, ct).ConfigureAwait(false);
        await InsertReglesEligibiliteAsync(conn, report, ct).ConfigureAwait(false);
        await InsertRubriqueBaremesAsync(conn, report, ct).ConfigureAwait(false);
        await InsertCotisationsAsync(conn, report, ct).ConfigureAwait(false);
        await InsertParametresAsync(conn, report, ct).ConfigureAwait(false);

        return report;
    }

    // -------------------------------------------------------------------------
    // Rubriques (10 au total : IEP_FONC, IEP_CONT, EXP_PEDAG, PAPP, QUALIF,
    //            DOC_PEDAG, ISSRP_45, ISSRP_30, ISSRP_15, IRG)
    // -------------------------------------------------------------------------
    private static async Task InsertRubriquesAsync(
        SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var rubriques = new (string Id, string Libelle, string Nature, string BaseCalcul,
            string Periodicite, string? PeriodiciteVersement, int OrdreCalcul,
            int EstImposable, int EstCotisable, string Description,
            int EstAffectableManuellement, int OccurrencesMultiples)[]
        {
            // IEP_FONC — Indemnité d'expérience professionnelle des fonctionnaires
            // (Q2-rev, 14/07/2026). Calcul : IE × VPI (indice d'échelon × valeur du
            // point) — composante échelon du traitement : TRT = TBASE + IEP_FONC.
            // Non affectable manuellement (V-2, J4E § 5) : composante structurelle
            // du traitement, pas une décision d'affectation.
            ("IEP_FONC", "Indemnité d'expérience professionnelle (fonctionnaires)",
             "GAIN", "INDICE_ECHELON", "MENSUELLE", null, 200, 1, 1,
             "IE × VPI — composante échelon du traitement (Art. 5 Décret 07-304)",
             0, 0),

            // IEP_CONT — Indemnité d'expérience professionnelle des contractuels
            // (Q2-rev). Prime d'ancienneté composite : TBASE × min(ANC_PUB × 1,4 %
            // + ANC_PRIV × 0,7 % ; 60 %). Le plafond 60 % porte sur le TAUX
            // composite, jamais sur les années. Paramètres : IEP_TAUX_PUBLIC_PCT,
            // IEP_TAUX_PRIVE_PCT, IEP_PLAFOND_PCT (table Parametres). Non
            // affectable manuellement (V-2) : symétrique structurel de IEP_FONC.
            ("IEP_CONT", "Indemnité d'expérience professionnelle (contractuels)",
             "GAIN", "TBASE", "MENSUELLE", null, 201, 1, 1,
             "TBASE × min(ANC_PUB × 1,4 % + ANC_PRIV × 0,7 % ; 60 %) (Art. 16 Décret 07-304)",
             0, 0),

            // EXP_PEDAG — Indemnité d'expérience pédagogique (Q2-rev). Distincte
            // de l'IEP : corps EN hors Intendance et Laboratoire. 4 % × TBASE ×
            // n° échelon. Bénéficiaires versionnés (direction/inspection au
            // 29/05/2012 via ReglesEligibilite). Affectable manuellement (V-2) :
            // indemnité conditionnée, cas d'usage type de la libre affectation.
            ("EXP_PEDAG", "Indemnité d'expérience pédagogique",
             "GAIN", "TBASE_ECHELON", "MENSUELLE", null, 210, 1, 1,
             "4 % × TBASE × n° échelon — corps EN hors Intendance/Laboratoire " +
             "(Art. 9 Décret 10-78 ; Art. 3 Décret 12-403 ; Art. 9 Décret 25-55)",
             1, 0),

            // PAPP — Prime d'amélioration des performances pédagogiques (INC-02,
            // Q-02 du 14/07/2026). 0–40 % du traitement selon notation ; calculée
            // mensuellement, servie trimestriellement ; imposable ET cotisable.
            // Affectable manuellement (V-2).
            ("PAPP", "Prime d'amélioration des performances pédagogiques (PAPP)",
             "GAIN", "TRAITEMENT", "MENSUELLE", "TRIMESTRIELLE", 220, 1, 1,
             "0–40 % du traitement selon notation (Art. 3 Décret 10-78 ; " +
             "Art. 3 Décret 12-403 ; Art. 3 Décret 25-55)",
             1, 0),

            // QUALIF — Indemnité de qualification (J3C §2, J3B RM-045). Taux par
            // tranche de catégorie (40 % / 45 %) — barème seedé séparément
            // (InsertRubriqueBaremesAsync). Même population que EXP_PEDAG/PAPP
            // (enseignants, éducation, orientation, alimentation, + intendance,
            // + direction/inspection depuis 29/05/2012) : aucune éligibilité
            // dédiée seedée ici, même simplification pilote que EXP_PEDAG/PAPP
            // (RM-040 : pas de condition = éligible partout ; l'éligibilité par
            // corps réelle est différée à Phase 5, cf. commentaire du repository).
            // Flags (16/07/2026) : même caractère que EXP_PEDAG (indemnité
            // conditionnée, pas structurelle) → affectable manuellement,
            // extension du principe V-2 non re-soumise à validation explicite
            // séparée — à signaler si contesté.
            ("QUALIF", "Indemnité de qualification",
             "GAIN", "TRAITEMENT", "MENSUELLE", null, 206, 1, 1,
             "TRT × (40 % si CAT ≤ 12 ; 45 % si CAT ≥ 13) — barème par catégorie " +
             "(Art. 7 Décret 11-373, rétroactif 2008 ; Art. 7 Décret 25-55)",
             1, 0),

            // DOC_PEDAG — Indemnité de documentation pédagogique (J3C §2, J3B
            // RM-046). Forfait par tranche de catégorie — barème seedé séparément.
            // Flags : même raisonnement que QUALIF ci-dessus.
            ("DOC_PEDAG", "Indemnité de documentation pédagogique",
             "GAIN", "FORFAIT", "MENSUELLE", null, 207, 1, 1,
             "Forfait par catégorie : 2 000 DA (≤10) / 2 500 DA (11-12) / 3 000 DA (≥13) " +
             "(Art. 8 Décret 10-78 ; Art. 5 Décret 11-373 ; Art. 8 Décret 25-55)",
             1, 0),

            // ISSRP_45 — Soutien scolaire 45 % (2025+). Affectable manuellement (V-2).
            ("ISSRP_45", "Soutien scolaire et remédiation pédagogique 45 % (2025+)",
             "GAIN", "TRAITEMENT", "MENSUELLE", null, 230, 1, 1,
             "Taux 45 % pour enseignants, direction, inspection, censeurs (Décret 25-55)",
             1, 0),

            // ISSRP_30 — Soutien scolaire 30 % (2025+). Affectable manuellement (V-2).
            ("ISSRP_30", "Soutien scolaire et remédiation pédagogique 30 % (2025+)",
             "GAIN", "TRAITEMENT", "MENSUELLE", null, 231, 1, 1,
             "Taux 30 % pour éducateurs non-enseignants, orientation, alimentation (Décret 25-55)",
             1, 0),

            // ISSRP_15 — Soutien scolaire 15 %.
            // 2008-2024 : taux unique 15 % pour tous les corps EN.
            // 2025+ : taux 15 % pour intendance / laboratoire / gestion financière.
            // Affectable manuellement (V-2).
            ("ISSRP_15", "Soutien scolaire et remédiation pédagogique 15 %",
             "GAIN", "TRAITEMENT", "MENSUELLE", null, 232, 1, 1,
             "Taux 15 % (historique 2008-2024 ou intendance 2025+) (Décrets 11-373, 25-55)",
             1, 0),

            // IRG — Impôt sur le Revenu Global (calculé via BaremeIRG 2008 + 2022
            // et 4 règles de période, V006-V007, décision Q-01). Non affectable
            // (D1) : pipeline exclusif, jamais une décision d'affectation.
            ("IRG", "Impôt sur le revenu global (IRG)",
             "IMPOT", "ASSIETTE_IMPOSABLE", "MENSUELLE", null, 600, 0, 0,
             "Barèmes 2008 & 2022 + 4 règles de période (avant 2020-06, 2020-06, 2021, 2022+)",
             0, 0),
        };

        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var rub in rubriques)
        {
            ct.ThrowIfCancellationRequested();
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
                o = rub.OrdreCalcul, ei = rub.EstImposable,
                ec = rub.EstCotisable, d = rub.Description,
                at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                h = $"h-rubrique-{rub.Id}",
                eam = rub.EstAffectableManuellement, om = rub.OccurrencesMultiples
            }, tx);
            inserted += n;
        }
        tx.Commit();
        r.Add("Rubriques", rubriques.Length, inserted);
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
    // Filiere/Corps réutilisent les identifiants déjà dérivés par
    // NomenclatureSeeder pour ce même corps (IDLS, via IDLS-G147 déjà seedé
    // par le CSV principal) — idempotent (ON CONFLICT DO NOTHING) : aucun
    // conflit si NomenclatureSeeder tourne aussi, même identité, même source.
    //
    // GrilleIndiciaire : pas de ligne pour la période « avant 01/03/2022 »
    // (indice = 0 dans la source — ces subdivisions hors catégorie n'avaient
    // pas d'indice avant cette date ; IndiceMin > 0 interdit de toute façon
    // la valeur 0, cf. CHECK V003).
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
    // RubriqueBaremes — QUALIF (taux par catégorie) + DOC_PEDAG (forfait par
    // catégorie). Une seule version par tranche (RM-104 : les taux 25/30 %
    // rétroactivement remplacés par 11-373 ne sont jamais sélectionnés pour un
    // calcul — inutile de les seeder).
    // -------------------------------------------------------------------------
    private static async Task InsertRubriqueBaremesAsync(
        SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var baremes = new (string Id, string RubriqueId, string BorneInf, string? BorneSup,
            string TypeValeur, string Valeur, string Source)[]
        {
            ("RB-QUALIF-CAT-LE12", "QUALIF", "1", "12", "TAUX", "0.40",
             "Art. 7 D.ex. 11-373 (rétroactif 2008) ; Art. 7 D.ex. 25-55"),
            ("RB-QUALIF-CAT-GE13", "QUALIF", "13", null, "TAUX", "0.45",
             "Art. 7 D.ex. 11-373 (rétroactif 2008) ; Art. 7 D.ex. 25-55"),

            ("RB-DOCPEDAG-CAT-LE10", "DOC_PEDAG", "1", "10", "MONTANT", "2000",
             "Art. 8 D.ex. 10-78 ; Art. 5 D.ex. 11-373 ; Art. 8 D.ex. 25-55"),
            ("RB-DOCPEDAG-CAT-11-12", "DOC_PEDAG", "11", "12", "MONTANT", "2500",
             "Art. 8 D.ex. 10-78 ; Art. 5 D.ex. 11-373 ; Art. 8 D.ex. 25-55"),
            ("RB-DOCPEDAG-CAT-GE13", "DOC_PEDAG", "13", null, "MONTANT", "3000",
             "Art. 8 D.ex. 10-78 ; Art. 5 D.ex. 11-373 ; Art. 8 D.ex. 25-55"),
        };

        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var b in baremes)
        {
            ct.ThrowIfCancellationRequested();
            var n = await c.ExecuteAsync("""
                INSERT INTO RubriqueBaremes
                    (Id, RubriqueId, Dimension, BorneInf, BorneSup, TypeValeur, Valeur,
                     DateEffet, Source, Hash, CreatedAt)
                VALUES
                    ($id, $r, 'CATEGORIE', $bi, $bs, $tv, $v, $de, $src, $h, $at)
                ON CONFLICT(Id) DO NOTHING;
                """,
                new
                {
                    id = b.Id, r = b.RubriqueId, bi = b.BorneInf,
                    bs = b.BorneSup ?? (object)DBNull.Value, tv = b.TypeValeur, v = b.Valeur,
                    de = IssrpPeriodeHistorique, src = b.Source, h = $"h-bareme-{b.Id}",
                    at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                }, tx);
            inserted += n;
        }
        tx.Commit();
        r.Add("RubriqueBaremes", baremes.Length, inserted);
    }

    // -------------------------------------------------------------------------
    // Cotisations — Q3b : SS 9 % (part ouvrière) + Mutuelle + Œuvres sociales
    // -------------------------------------------------------------------------
    private static async Task InsertCotisationsAsync(
        SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var cotisations = new (string Id, string Code, string Libelle, string Type,
            double? Taux, string AssietteRef, int EstRetenue, string DateEffet, string Source)[]
        {
            // SS — Sécurité Sociale, part ouvrière (Q3b).
            ("SS-2007-01-01", "SS",
             "Sécurité sociale (part ouvrière)",
             "OBLIGATOIRE_SALARIALE", 0.09, "ASSIETTE_COTISABLE", 1,
             "2007-01-01", "Loi 07-308 — art. 16"),
            // MUTUELLE — facultative, montant fixe par agent (Q3b).
            ("MUTUELLE-2007-01-01", "MUTUELLE",
             "Cotisation mutuelle (facultative)",
             "FACULTATIVE", null, "MONTANT_FIXE", 1,
             "2007-01-01", "Régime interne Éducation"),
            // OEUVRES_SOCIALES — facultative, montant fixe par agent (Q3b).
            ("OEUVRES-2007-01-01", "OEUVRES_SOCIALES",
             "Œuvres sociales (facultative)",
             "FACULTATIVE", null, "MONTANT_FIXE", 1,
             "2007-01-01", "Décret 82-304 — art. 8"),
        };

        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var cot in cotisations)
        {
            ct.ThrowIfCancellationRequested();
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
                tx = cot.Taux, ar = cot.AssietteRef, er = cot.EstRetenue,
                de = cot.DateEffet, src = cot.Source,
                h = $"h-cotisation-{cot.Code}",
                at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            }, tx);
            inserted += n;
        }
        tx.Commit();
        r.Add("Cotisations", cotisations.Length, inserted);
    }

    // -------------------------------------------------------------------------
    // Paramètres système (Q9b : ARRONDI_MODE)
    // -------------------------------------------------------------------------
    private static async Task InsertParametresAsync(
        SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var parametres = new (string Id, string Cle, string Valeur, string Type,
            string Description, string DateEffet)[]
        {
            // Q9b : défaut retenu "au dinar le plus proche", paramétrable.
            ("P-ARRONDI-MODE", "ARRONDI_MODE", "DINAR_PLUS_PROCHE", "TEXT",
             "Mode d'arrondi par défaut (Q9b) — DINAR_PLUS_PROCHE | DIZAINE | CENTIME",
             "2007-01-01"),
            ("P-ARRONDI-PRECISION", "ARRONDI_PRECISION", "1", "INT",
             "Précision d'arrondi (DA) — défaut 1 (au dinar)",
             "2007-01-01"),
            ("P-VALEUR-POINT-DEFAUT", "VALEUR_POINT_DEFAUT", "45", "INT",
             "Valeur du point indiciaire par défaut (DA) — avant seed de ValeurPoint",
             "2007-01-01"),
            ("P-BASE-PAPP", "BASE_PAPP", "0.40", "REAL",
             "BASE_PAPP — Taux de base de la PAPP (40 %) avant application de la notation (Art. 3 Décret 10-78)",
             "2007-01-01"),
            ("P-NOTE-MAX-PAPP", "NOTE_MAX_PAPP", "20", "INT",
             "NOTE_MAX_PAPP — Note maximale de notation PAPP (20) (J3C §2)",
             "2007-01-01"),
            ("P-PLAFOND-LISSAGE-GENERAL", "PLAFOND_LISSAGE_GENERAL", "35000", "INT",
             "PLAFOND_LISSAGE_GENERAL — Plafond du lissage général IRG (DA) (CALCUL IRG ALGERIE.txt)",
             "2007-01-01"),
            ("P-SEUIL-EXO-IRG", "SEUIL_EXONERATION_IRG", "30000", "INT",
             "Seuil d'exonération IRG par défaut (DA) — avant seed de IRGReglesPeriode",
             "2007-01-01"),
            // IEP_CONT (Q2-rev, 14/07/2026) : taux composite d'ancienneté des
            // contractuels. Le plafond porte sur le TAUX (60 % du TB max),
            // jamais sur les années de service.
            ("P-IEP-TAUX-PUBLIC", "IEP_TAUX_PUBLIC_PCT", "1.4", "REAL",
             "IEP_CONT — % par année d'ancienneté de service public (Art. 16 Décret 07-304)",
             "2007-01-01"),
            ("P-IEP-TAUX-PRIVE", "IEP_TAUX_PRIVE_PCT", "0.7", "REAL",
             "IEP_CONT — % par année d'ancienneté de service privé (Art. 16 Décret 07-304)",
             "2007-01-01"),
            ("P-IEP-PLAFOND", "IEP_PLAFOND_PCT", "60", "REAL",
             "IEP_CONT — plafond du taux composite (% du traitement de base)",
             "2007-01-01"),
        };

        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var p in parametres)
        {
            ct.ThrowIfCancellationRequested();
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
                h = $"h-parametre-{p.Cle}",
                at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            }, tx);
            inserted += n;
        }
        tx.Commit();
        r.Add("Parametres", parametres.Length, inserted);
    }
}
