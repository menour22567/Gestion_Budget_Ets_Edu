using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;

namespace PaieEducation.Tools.Seeding;

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
    // ----- Constantes de la matrice ISSRP (issue de ISSRP_Corrige_26526.txt) --
    // Avant 2025-01-01 : taux unique 15 % pour TOUS les corps EN.
    // À partir de 2025-01-01 : 3 taux selon le groupe d'appartenance du corps.
    //   * 45 % : enseignants + direction + inspection + censeurs
    //            + conseillers issus du corps enseignant
    //            + grades de promotion d'origine enseignante.
    //   * 30 % : éducateurs non issus du corps enseignant
    //            + orientation/guidance + alimentation scolaire
    //            + inspecteurs alimentation + inspecteurs orientation.
    //   * 15 % : intendance + laboratoire + inspecteurs gestion financière.

    private const string IssrpPeriodeHistorique = "2008-01-01";
    private const string IssrpPeriodeActuelle = "2025-01-01";
    private const double IssrpTauxHistorique = 0.15;
    private const double IssrpTaux45 = 0.45;
    private const double IssrpTaux30 = 0.30;
    private const double IssrpTaux15 = 0.15;

    /// <summary>Codes de corps (acronymes générés par <see cref="NomenclatureSeeder.CodeFromCorpsLibelle"/>)
    /// qui tombent dans le groupe 45 % ISSRP (à partir de 2025-01-01).</summary>
    internal static readonly string[] Issrp45CorpsCodes = new[]
    {
        "CPDE",    // Corps des Professeurs d'Education
        "CDDL",    // Corps des Directeurs (d'établissement d'enseignement) — ex
        "CDC",     // Corps des Censeurs (lycée / primaire / moyen / secondaire)
        "CI",      // Corps de l'Inspection
    };

    /// <summary>Codes de corps qui tombent dans le groupe 30 %.</summary>
    internal static readonly string[] Issrp30CorpsCodes = new[]
    {
        "CDAE",    // Corps des Adjoints de l'Education
        "CDE",     // Corps des Surveillants / Conseillers de l'Education
        "CDOS",    // Corps des Conseillers de l'orientation
        "CDCLG",   // Corps des Conseillers en alimentation scolaire
    };

    /// <summary>Codes de corps qui tombent dans le groupe 15 %.</summary>
    internal static readonly string[] Issrp15CorpsCodes = new[]
    {
        // Intendance, laboratoire, gestion financière — acronymes à confirmer
        // contre le CSV réel lors de l'extension. Placeholders pour la V1.
        "CDI",     // Corps des Intendants (placeholder)
        "CDL",     // Corps de Laboratoire (placeholder)
    };

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
        await InsertReglesEligibiliteAsync(conn, report, ct).ConfigureAwait(false);
        await InsertCotisationsAsync(conn, report, ct).ConfigureAwait(false);
        await InsertParametresAsync(conn, report, ct).ConfigureAwait(false);

        return report;
    }

    // -------------------------------------------------------------------------
    // Rubriques (8 au total : IEP_FONC, IEP_CONT, EXP_PEDAG, PAPP,
    //            ISSRP_45, ISSRP_30, ISSRP_15, IRG)
    // -------------------------------------------------------------------------
    private static async Task InsertRubriquesAsync(
        SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var rubriques = new (string Id, string Libelle, string Nature, string BaseCalcul,
            string Periodicite, string? PeriodiciteVersement, int OrdreCalcul,
            int EstImposable, int EstCotisable, string Description)[]
        {
            // IEP_FONC — Indemnité d'expérience professionnelle des fonctionnaires
            // (Q2-rev, 14/07/2026). Calcul : IE × VPI (indice d'échelon × valeur du
            // point) — composante échelon du traitement : TRT = TBASE + IEP_FONC.
            ("IEP_FONC", "Indemnité d'expérience professionnelle (fonctionnaires)",
             "GAIN", "INDICE_ECHELON", "MENSUELLE", null, 200, 1, 1,
             "IE × VPI — composante échelon du traitement (Art. 5 Décret 07-304)"),

            // IEP_CONT — Indemnité d'expérience professionnelle des contractuels
            // (Q2-rev). Prime d'ancienneté composite : TBASE × min(ANC_PUB × 1,4 %
            // + ANC_PRIV × 0,7 % ; 60 %). Le plafond 60 % porte sur le TAUX
            // composite, jamais sur les années. Paramètres : IEP_TAUX_PUBLIC_PCT,
            // IEP_TAUX_PRIVE_PCT, IEP_PLAFOND_PCT (table Parametres).
            ("IEP_CONT", "Indemnité d'expérience professionnelle (contractuels)",
             "GAIN", "TBASE", "MENSUELLE", null, 201, 1, 1,
             "TBASE × min(ANC_PUB × 1,4 % + ANC_PRIV × 0,7 % ; 60 %) (Art. 16 Décret 07-304)"),

            // EXP_PEDAG — Indemnité d'expérience pédagogique (Q2-rev). Distincte
            // de l'IEP : corps EN hors Intendance et Laboratoire. 4 % × TBASE ×
            // n° échelon. Bénéficiaires versionnés (direction/inspection au
            // 29/05/2012 via ReglesEligibilite).
            ("EXP_PEDAG", "Indemnité d'expérience pédagogique",
             "GAIN", "TBASE_ECHELON", "MENSUELLE", null, 210, 1, 1,
             "4 % × TBASE × n° échelon — corps EN hors Intendance/Laboratoire " +
             "(Art. 9 Décret 10-78 ; Art. 3 Décret 12-403 ; Art. 9 Décret 25-55)"),

            // PAPP — Prime d'amélioration des performances pédagogiques (INC-02,
            // Q-02 du 14/07/2026). 0–40 % du traitement selon notation ; calculée
            // mensuellement, servie trimestriellement ; imposable ET cotisable.
            ("PAPP", "Prime d'amélioration des performances pédagogiques (PAPP)",
             "GAIN", "TRAITEMENT", "MENSUELLE", "TRIMESTRIELLE", 220, 1, 1,
             "0–40 % du traitement selon notation (Art. 3 Décret 10-78 ; " +
             "Art. 3 Décret 12-403 ; Art. 3 Décret 25-55)"),

            // ISSRP_45 — Soutien scolaire 45 % (2025+).
            ("ISSRP_45", "Soutien scolaire et remédiation pédagogique 45 % (2025+)",
             "GAIN", "TRAITEMENT", "MENSUELLE", null, 230, 1, 1,
             "Taux 45 % pour enseignants, direction, inspection, censeurs (Décret 25-55)"),

            // ISSRP_30 — Soutien scolaire 30 % (2025+).
            ("ISSRP_30", "Soutien scolaire et remédiation pédagogique 30 % (2025+)",
             "GAIN", "TRAITEMENT", "MENSUELLE", null, 231, 1, 1,
             "Taux 30 % pour éducateurs non-enseignants, orientation, alimentation (Décret 25-55)"),

            // ISSRP_15 — Soutien scolaire 15 %.
            // 2008-2024 : taux unique 15 % pour tous les corps EN.
            // 2025+ : taux 15 % pour intendance / laboratoire / gestion financière.
            ("ISSRP_15", "Soutien scolaire et remédiation pédagogique 15 %",
             "GAIN", "TRAITEMENT", "MENSUELLE", null, 232, 1, 1,
             "Taux 15 % (historique 2008-2024 ou intendance 2025+) (Décrets 11-373, 25-55)"),

            // IRG — Impôt sur le Revenu Global (calculé via BaremeIRG 2008 + 2022
            // et 4 règles de période, V006-V007, décision Q-01).
            ("IRG", "Impôt sur le revenu global (IRG)",
             "IMPOT", "ASSIETTE_IMPOSABLE", "MENSUELLE", null, 600, 0, 0,
             "Barèmes 2008 & 2022 + 4 règles de période (avant 2020-06, 2020-06, 2021, 2022+)"),
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
                     CreatedAt, Source, Hash)
                VALUES
                    ($id, $l, $n, $b, $p, $pv, $o, $ei, $ec, $d, 1,
                     $at, 'J2.c/J3 — seed réglementaire', $h)
                ON CONFLICT(Id) DO NOTHING;
                """;
            var n = await c.ExecuteAsync(sql, new
            {
                id = rub.Id, l = rub.Libelle, n = rub.Nature, b = rub.BaseCalcul,
                p = rub.Periodicite, pv = rub.PeriodiciteVersement ?? (object)DBNull.Value,
                o = rub.OrdreCalcul, ei = rub.EstImposable,
                ec = rub.EstCotisable, d = rub.Description,
                at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                h = $"h-rubrique-{rub.Id}"
            }, tx);
            inserted += n;
        }
        tx.Commit();
        r.Add("Rubriques", rubriques.Length, inserted);
    }

    // -------------------------------------------------------------------------
    // ReglesEligibilite — matrice ISSRP corps → taux
    // -------------------------------------------------------------------------
    private static async Task InsertReglesEligibiliteAsync(
        SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var inserted = 0;
        using var tx = c.BeginTransaction();

        // R3 (V009) : `Critere` (TEXT) remplacé par `CritereId` (FK vers CriteresEligibilite).
        // Le critère 'CORPS' est désormais porté par le code catalogue CriteresEligibilite
        // (seed V009) — la valeur du critère reste 'CORPS' mais elle désigne un Id de
        // dictionnaire, pas une valeur littérale d'un CHECK.

        // ISSRP_45 : éligible pour les corps du groupe 45 %.
        foreach (var corpsCode in Issrp45CorpsCodes)
        {
            ct.ThrowIfCancellationRequested();
            var n = await c.ExecuteAsync("""
                INSERT INTO ReglesEligibilite
                    (Id, RubriqueId, CritereId, Operateur, Valeur, DateEffet, Source, Hash, CreatedAt)
                VALUES
                    ($id, $r, 'CORPS', '=', $v, $de, 'J2.c — ISSRP 45 %', $h, $at)
                ON CONFLICT(Id) DO NOTHING;
                """,
                new
                {
                    id = $"RE-ISSRP-45-{corpsCode}",
                    r = "ISSRP_45", v = corpsCode,
                    de = IssrpPeriodeActuelle,
                    h = $"h-re-issrp-45-{corpsCode}",
                    at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                }, tx);
            inserted += n;
        }

        // ISSRP_30 : éligible pour les corps du groupe 30 %.
        foreach (var corpsCode in Issrp30CorpsCodes)
        {
            ct.ThrowIfCancellationRequested();
            var n = await c.ExecuteAsync("""
                INSERT INTO ReglesEligibilite
                    (Id, RubriqueId, CritereId, Operateur, Valeur, DateEffet, Source, Hash, CreatedAt)
                VALUES
                    ($id, $r, 'CORPS', '=', $v, $de, 'J2.c — ISSRP 30 %', $h, $at)
                ON CONFLICT(Id) DO NOTHING;
                """,
                new
                {
                    id = $"RE-ISSRP-30-{corpsCode}",
                    r = "ISSRP_30", v = corpsCode,
                    de = IssrpPeriodeActuelle,
                    h = $"h-re-issrp-30-{corpsCode}",
                    at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                }, tx);
            inserted += n;
        }

        // ISSRP_15 : éligible pour les corps du groupe 15 %.
        foreach (var corpsCode in Issrp15CorpsCodes)
        {
            ct.ThrowIfCancellationRequested();
            var n = await c.ExecuteAsync("""
                INSERT INTO ReglesEligibilite
                    (Id, RubriqueId, CritereId, Operateur, Valeur, DateEffet, Source, Hash, CreatedAt)
                VALUES
                    ($id, $r, 'CORPS', '=', $v, $de, 'J2.c — ISSRP 15 %', $h, $at)
                ON CONFLICT(Id) DO NOTHING;
                """,
                new
                {
                    id = $"RE-ISSRP-15-{corpsCode}",
                    r = "ISSRP_15", v = corpsCode,
                    de = IssrpPeriodeActuelle,
                    h = $"h-re-issrp-15-{corpsCode}",
                    at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                }, tx);
            inserted += n;
        }

        tx.Commit();
        r.Add("ReglesEligibilite", Issrp45CorpsCodes.Length + Issrp30CorpsCodes.Length + Issrp15CorpsCodes.Length, inserted);
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
            ("P-SEUIL-EXO-IRG", "SEUIL_EXONERATION_IRG_DEFAUT", "30000", "INT",
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
