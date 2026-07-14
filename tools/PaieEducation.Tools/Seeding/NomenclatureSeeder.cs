using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Tools.Seeding.Models;

namespace PaieEducation.Tools.Seeding;

/// <summary>
/// Seeder de la **nomenclature** (filières, types de contrat, types de
/// personnel, échelons, catégories, corps, grades, valeur du point,
/// grille indiciaire). À partir d'un CSV <see cref="CsvCascadeParser"/>.
/// </summary>
/// <remarks>
/// <para>Le seeder est **idempotent** : il utilise <c>INSERT OR IGNORE</c>
/// partout. Ré-exécuté plusieurs fois, il ne duplique pas les lignes.</para>
/// <para>Ordre d'insertion (FK + idempotence) :
/// <list type="number">
///   <item>Filieres</item>
///   <item>TypesContrat</item>
///   <item>TypesPersonnel</item>
///   <item>Echelons (1..12) — codé en dur, le CSV ne les porte pas</item>
///   <item>Categories (1..17 + HC-S1 + HC-S2) — codé en dur</item>
///   <item>Corps (FK → Filieres)</item>
///   <item>Grades (FK → Corps)</item>
///   <item>ValeurPoint (45 DA, depuis 2007-01-01) — codé en dur</item>
///   <item>GrilleIndiciaire (FK → Categories, 4 périodes)</item>
/// </list>
/// </para>
/// <para>Il suppose que la **base est déjà migrée** (V001-V007 appliqués) :
/// la table existe avec le bon schéma. Ce seeder ne fait pas de migration.</para>
/// </remarks>
public sealed class NomenclatureSeeder
{
    // ----- Constantes codées en dur (non réglementaires) -------------------------
    // 12 échelons (V002 schéma, borne CHECK BETWEEN 1 AND 12).
    private static readonly int[] _echelonNumeros = Enumerable.Range(1, 12).ToArray();

    // 19 catégories (V002 schéma, borne CHECK BETWEEN 1 AND 19).
    // 1..17 = catégories ordinaires ; HC-S1 = Hors Catégorie Spécial 1
    // (niveau 18) ; HC-S2 = Hors Catégorie Spécial 2 (niveau 19).
    private static readonly (string Id, int Niveau, bool HorsCategorie, string Libelle)[] _categories =
    {
        ("1", 1, false, "1ère catégorie"),
        ("2", 2, false, "2ème catégorie"),
        ("3", 3, false, "3ème catégorie"),
        ("4", 4, false, "4ème catégorie"),
        ("5", 5, false, "5ème catégorie"),
        ("6", 6, false, "6ème catégorie"),
        ("7", 7, false, "7ème catégorie"),
        ("8", 8, false, "8ème catégorie"),
        ("9", 9, false, "9ème catégorie"),
        ("10", 10, false, "10ème catégorie"),
        ("11", 11, false, "11ème catégorie"),
        ("12", 12, false, "12ème catégorie"),
        ("13", 13, false, "13ème catégorie"),
        ("14", 14, false, "14ème catégorie"),
        ("15", 15, false, "15ème catégorie"),
        ("16", 16, false, "16ème catégorie"),
        ("17", 17, false, "17ème catégorie"),
        ("HC-S1", 18, true, "Hors Catégorie Spécial 1"),
        ("HC-S2", 19, true, "Hors Catégorie Spécial 2"),
    };

    // Valeur du point indiciaire en DZD (Q1 : 45 DA par défaut, paramétrable).
    private const string ValeurPointDateEffet = "2007-01-01";
    private const double ValeurPointDZD = 45.0;
    private const string ValeurPointVersion = "2007";
    private const string ValeurPointSource = "Décret 07-308";

    /// <summary>Génère un code stable pour un corps depuis son libellé complet
    /// (le CSV ne porte pas de code, juste un libellé long). Heuristique :
    /// on prend les initiales de chaque mot significatif, plafonné à 10 chars.</summary>
    internal static string CodeFromCorpsLibelle(string libelle)
    {
        // Enlève "Corps des/de la/du/de l'" en tête.
        var s = libelle.Trim();
        foreach (var prefix in new[] { "Corps des ", "Corps de la ", "Corps du ", "Corps de l’", "Corps de l'" })
        {
            if (s.StartsWith(prefix, StringComparison.Ordinal))
            {
                s = s[prefix.Length..];
                break;
            }
        }
        // Acronyme : 1ère lettre de chaque mot, max 10.
        var sb = new System.Text.StringBuilder();
        foreach (var word in s.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var c = char.ToUpperInvariant(word[0]);
            if (char.IsLetter(c)) sb.Append(c);
            if (sb.Length >= 10) break;
        }
        var acro = sb.ToString();
        return string.IsNullOrEmpty(acro) ? "X" : acro;
    }

    /// <summary>Génère un code stable pour un grade depuis son libellé.</summary>
    internal static string CodeFromGradeLibelle(string libelle, string corpsCode, int numOrd)
    {
        // Format : <CORPS>-G<NN> — stable, lisible, dérivé du num_ord du CSV.
        // On garde le rang de l'ordre (1..190) pour assurer l'unicité.
        return $"{corpsCode}-G{numOrd:D3}";
    }

    /// <summary>
    /// Seeder complet : prend les lignes CSV en mémoire, agrège, et écrit
    /// dans la base ouverte. La transaction est **par bloc logique** (une
    /// par table) pour isoler les échecs.
    /// </summary>
    public async Task<SeedReport> SeedAsync(
        SqliteConnection conn,
        IReadOnlyList<CascadeRow> rows,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentNullException.ThrowIfNull(rows);
        if (conn.State != System.Data.ConnectionState.Open)
            throw new InvalidOperationException("La connexion doit être ouverte.");

        var report = new SeedReport(rows.Count);

        await InsertFilieresAsync(conn, rows, report, ct).ConfigureAwait(false);
        await InsertTypesContratAsync(conn, rows, report, ct).ConfigureAwait(false);
        await InsertTypesPersonnelAsync(conn, rows, report, ct).ConfigureAwait(false);
        await InsertEchelonsAsync(conn, report, ct).ConfigureAwait(false);
        await InsertCategoriesAsync(conn, report, ct).ConfigureAwait(false);
        await InsertCorpsAsync(conn, rows, report, ct).ConfigureAwait(false);
        await InsertGradesAsync(conn, rows, report, ct).ConfigureAwait(false);
        await InsertValeurPointAsync(conn, report, ct).ConfigureAwait(false);
        await InsertGrilleIndiciaireAsync(conn, rows, report, ct).ConfigureAwait(false);

        return report;
    }

    // -------------------------------------------------------------------------
    // Filieres
    // -------------------------------------------------------------------------
    private static async Task InsertFilieresAsync(
        SqliteConnection c, IReadOnlyList<CascadeRow> rows, SeedReport r, CancellationToken ct)
    {
        var distinct = rows.Select(x => x.TypeFiliere).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var f in distinct)
        {
            ct.ThrowIfCancellationRequested();
            var id = NormalizeId(f);
            var libelle = f;
            var sql = """
                INSERT INTO Filieres (Id, Libelle, Actif, CreatedAt, Source, Hash)
                VALUES ($id, $l, 1, $at, 'Cascade_Corps_Grades_*.csv', $h)
                ON CONFLICT(Id) DO NOTHING;
                """;
            var n = await c.ExecuteAsync(sql, new
            {
                id, l = libelle,
                at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                h = $"h-filiere-{id}"
            }, tx);
            inserted += n;
        }
        tx.Commit();
        r.Add("Filieres", distinct.Count, inserted);
    }

    // -------------------------------------------------------------------------
    // TypesContrat
    // -------------------------------------------------------------------------
    private static async Task InsertTypesContratAsync(
        SqliteConnection c, IReadOnlyList<CascadeRow> rows, SeedReport r, CancellationToken ct)
    {
        var distinct = rows.Select(x => x.TypeContrat).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var t in distinct)
        {
            ct.ThrowIfCancellationRequested();
            var id = NormalizeId(t);
            var libelle = t; // Le CSV ne porte pas de libellé distinct.
            var sql = """
                INSERT INTO TypesContrat (Id, Libelle, Actif, CreatedAt, Source, Hash)
                VALUES ($id, $l, 1, $at, 'Cascade_Corps_Grades_*.csv', $h)
                ON CONFLICT(Id) DO NOTHING;
                """;
            var n = await c.ExecuteAsync(sql, new
            {
                id, l = libelle,
                at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                h = $"h-typecontrat-{id}"
            }, tx);
            inserted += n;
        }
        tx.Commit();
        r.Add("TypesContrat", distinct.Count, inserted);
    }

    // -------------------------------------------------------------------------
    // TypesPersonnel
    // -------------------------------------------------------------------------
    private static async Task InsertTypesPersonnelAsync(
        SqliteConnection c, IReadOnlyList<CascadeRow> rows, SeedReport r, CancellationToken ct)
    {
        var distinct = rows.Select(x => x.TypePersonnel).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var t in distinct)
        {
            ct.ThrowIfCancellationRequested();
            var id = NormalizeId(t);
            var libelle = t;
            var sql = """
                INSERT INTO TypesPersonnel (Id, Libelle, Actif, CreatedAt, Source, Hash)
                VALUES ($id, $l, 1, $at, 'Cascade_Corps_Grades_*.csv', $h)
                ON CONFLICT(Id) DO NOTHING;
                """;
            var n = await c.ExecuteAsync(sql, new
            {
                id, l = libelle,
                at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                h = $"h-typepersonnel-{id}"
            }, tx);
            inserted += n;
        }
        tx.Commit();
        r.Add("TypesPersonnel", distinct.Count, inserted);
    }

    // -------------------------------------------------------------------------
    // Echelons (codé en dur, 1..12)
    // -------------------------------------------------------------------------
    private static async Task InsertEchelonsAsync(
        SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var n in _echelonNumeros)
        {
            ct.ThrowIfCancellationRequested();
            var id = n.ToString(CultureInfo.InvariantCulture);
            var libelle = $"{n}ème échelon";
            var sql = """
                INSERT INTO Echelons (Id, Numero, Libelle, Actif, CreatedAt, Source, Hash)
                VALUES ($id, $n, $l, 1, $at, 'codé en dur — barème indiciaire', $h)
                ON CONFLICT(Id) DO NOTHING;
                """;
            var x = await c.ExecuteAsync(sql, new
            {
                id, n, l = libelle,
                at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                h = $"h-echelon-{id}"
            }, tx);
            inserted += x;
        }
        tx.Commit();
        r.Add("Echelons", _echelonNumeros.Length, inserted);
    }

    // -------------------------------------------------------------------------
    // Categories (codé en dur, 19 valeurs)
    // -------------------------------------------------------------------------
    private static async Task InsertCategoriesAsync(
        SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var (id, niveau, horsCat, libelle) in _categories)
        {
            ct.ThrowIfCancellationRequested();
            var sql = """
                INSERT INTO Categories (Id, Niveau, HorsCategorie, Libelle, Actif, CreatedAt, Source, Hash)
                VALUES ($id, $n, $hc, $l, 1, $at, 'codé en dur — barème indiciaire', $h)
                ON CONFLICT(Id) DO NOTHING;
                """;
            var x = await c.ExecuteAsync(sql, new
            {
                id, n = niveau, hc = horsCat ? 1 : 0, l = libelle,
                at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                h = $"h-cat-{id}"
            }, tx);
            inserted += x;
        }
        tx.Commit();
        r.Add("Categories", _categories.Length, inserted);
    }

    // -------------------------------------------------------------------------
    // Corps (FK → Filieres)
    // -------------------------------------------------------------------------
    private static async Task InsertCorpsAsync(
        SqliteConnection c, IReadOnlyList<CascadeRow> rows, SeedReport r, CancellationToken ct)
    {
        var distinct = rows
            .GroupBy(x => x.CorpsFiliere)
            .Select(g => (CorpsFiliere: g.Key, TypeFiliere: g.First().TypeFiliere))
            .OrderBy(x => x.CorpsFiliere, StringComparer.Ordinal)
            .ToList();

        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var (corps, filiere) in distinct)
        {
            ct.ThrowIfCancellationRequested();
            var id = CodeFromCorpsLibelle(corps);
            var filiereId = NormalizeId(filiere);
            var sql = """
                INSERT INTO Corps (Id, Libelle, FiliereId, Actif, CreatedAt, Source, Hash)
                VALUES ($id, $l, $fid, 1, $at, 'Cascade_Corps_Grades_*.csv', $h)
                ON CONFLICT(Id) DO NOTHING;
                """;
            var n = await c.ExecuteAsync(sql, new
            {
                id, l = corps, fid = filiereId,
                at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                h = $"h-corps-{id}"
            }, tx);
            inserted += n;
        }
        tx.Commit();
        r.Add("Corps", distinct.Count, inserted);
    }

    // -------------------------------------------------------------------------
    // Grades (FK → Corps)
    // -------------------------------------------------------------------------
    private static async Task InsertGradesAsync(
        SqliteConnection c, IReadOnlyList<CascadeRow> rows, SeedReport r, CancellationToken ct)
    {
        // Ordonner par NumOrd pour que l'ordre des grades soit stable.
        var ordered = rows.OrderBy(x => x.NumOrd).ToList();

        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var row in ordered)
        {
            ct.ThrowIfCancellationRequested();
            var corpsId = CodeFromCorpsLibelle(row.CorpsFiliere);
            var id = CodeFromGradeLibelle(row.Grade, corpsId, row.NumOrd);
            var sql = """
                INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, Actif, CreatedAt, Source, Hash)
                VALUES ($id, $l, $cid, $o, 1, $at, 'Cascade_Corps_Grades_*.csv', $h)
                ON CONFLICT(Id) DO NOTHING;
                """;
            var n = await c.ExecuteAsync(sql, new
            {
                id, l = row.Grade, cid = corpsId, o = row.NumOrd,
                at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                h = $"h-grade-{id}"
            }, tx);
            inserted += n;
        }
        tx.Commit();
        r.Add("Grades", ordered.Count, inserted);
    }

    // -------------------------------------------------------------------------
    // ValeurPoint (45 DA depuis 2007-01-01)
    // -------------------------------------------------------------------------
    private static async Task InsertValeurPointAsync(
        SqliteConnection c, SeedReport r, CancellationToken ct)
    {
        var id = $"VP-{ValeurPointDateEffet}";
        var sql = """
            INSERT INTO ValeurPoint (Id, DateEffet, DateFin, Valeur, Version, Source, Hash, CreatedAt)
            VALUES ($id, $de, NULL, $v, $ver, $src, $h, $at)
            ON CONFLICT(Id) DO NOTHING;
            """;
        using var tx = c.BeginTransaction();
        var n = await c.ExecuteAsync(sql, new
        {
            id,
            de = ValeurPointDateEffet,
            v = ValeurPointDZD,
            ver = ValeurPointVersion,
            src = ValeurPointSource,
            h = "h-valeurpoint-2007",
            at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        }, tx);
        tx.Commit();
        r.Add("ValeurPoint", 1, n);
    }

    // -------------------------------------------------------------------------
    // GrilleIndiciaire (FK → Categories, 4 périodes)
    // -------------------------------------------------------------------------
    private static async Task InsertGrilleIndiciaireAsync(
        SqliteConnection c, IReadOnlyList<CascadeRow> rows, SeedReport r, CancellationToken ct)
    {
        // Agréger les indices par catégorie (toutes périodes confondues).
        // Pour chaque catégorie présente dans le CSV, on insère 4 lignes
        // (1 par période). Si une catégorie a plusieurs grades (NumOrd
        // différents) avec des indices identiques, c'est OK (UNIQUE sur
        // (CategorieId, DateEffet)).
        var byCategorie = rows
            .GroupBy(x => x.Categorie)
            .ToDictionary(g => g.Key, g => g.First()); // tous les grades
                                                         // d'une même catégorie
                                                         // partagent les mêmes
                                                         // indices (le CSV est
                                                         // cohérent là-dessus).

        var inserted = 0;
        var plan = new (string DateEffet, string? DateFin, int Indice, string Version)[]
        {
            (ValeurPointDateEffet, "2022-02-28", 0, "2007"),   // valeur par défaut 0 → sera écrasée ci-dessous
        };

        using var tx = c.BeginTransaction();
        foreach (var (catNum, sample) in byCategorie)
        {
            ct.ThrowIfCancellationRequested();
            var catId = catNum.ToString(CultureInfo.InvariantCulture);
            var periodes = new (string DateEffet, string? DateFin, int Indice, string Version)[]
            {
                (ValeurPointDateEffet, "2022-02-28", sample.IndiceAv2022_03, "2007"),
                ("2022-03-01", "2022-12-31",        sample.IndiceAp2022_03, "2022-03"),
                ("2023-01-01", "2023-12-31",        sample.IndiceAp2023_01, "2023"),
                ("2024-01-01", null,                sample.IndiceAp2024_01, "2024"),
            };
            foreach (var (de, df, indice, version) in periodes)
            {
                var id = $"GI-{catId}-{de}";
                var sql = """
                    INSERT INTO GrilleIndiciaire (Id, CategorieId, DateEffet, DateFin, IndiceMin, Version, Source, Hash, CreatedAt)
                    VALUES ($id, $cid, $de, $df, $i, $ver, 'Cascade_Corps_Grades_*.csv', $h, $at)
                    ON CONFLICT(Id) DO NOTHING;
                    """;
                var n = await c.ExecuteAsync(sql, new
                {
                    id, cid = catId, de, df = df ?? (object)DBNull.Value, i = indice, ver = version,
                    h = $"h-grille-{id}",
                    at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                }, tx);
                inserted += n;
            }
        }
        tx.Commit();
        r.Add("GrilleIndiciaire", byCategorie.Count * 4, inserted);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Normalise un libellé en Id stable : retire accents, met en MAJ,
    /// remplace les non-alphanumériques par _, déduplique les underscores,
    /// plafonne à 40 chars.</summary>
    private static string NormalizeId(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        var lastWasUnderscore = true;
        foreach (var ch in s.Trim())
        {
            var upper = char.ToUpperInvariant(ch);
            if (upper is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                sb.Append(upper);
                lastWasUnderscore = false;
            }
            else if (!lastWasUnderscore)
            {
                sb.Append('_');
                lastWasUnderscore = true;
            }
        }
        var s2 = sb.ToString().TrimEnd('_');
        if (s2.Length > 40) s2 = s2[..40].TrimEnd('_');
        return string.IsNullOrEmpty(s2) ? "X" : s2;
    }
}

/// <summary>
/// Rapport d'un <see cref="NomenclatureSeeder.SeedAsync"/>. Une ligne par
/// table touchée. <c>Lues</c> = nombre de lignes distinctes dans la source
/// (avant insert) ; <c>Inserees</c> = nombre de lignes effectivement insérées
/// (les autres étaient déjà là → <c>INSERT OR IGNORE</c>).
/// </summary>
public sealed class SeedReport
{
    public int Lues { get; }
    public IReadOnlyList<SeedTableReport> Tables => _tables;

    private readonly List<SeedTableReport> _tables = new();

    public SeedReport(int lues) { Lues = lues; }

    internal void Add(string table, int lues, int inserees)
    {
        _tables.Add(new SeedTableReport(table, lues, inserees));
    }
}

public sealed record SeedTableReport(string Table, int Lues, int Inserees);
