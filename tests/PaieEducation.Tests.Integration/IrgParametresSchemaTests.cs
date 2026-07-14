using Microsoft.Data.Sqlite;
using PaieEducation.Persistence.Migrations;

namespace PaieEducation.Tests.Integration;

/// <summary>
/// Tests d'intégration du schéma V006 — Barème IRG, règles de période, paramètres.
/// Couvre :
///   - création des 4 tables ;
///   - CHECK sur Taux (0..1) et Type (enum) ;
///   - UNIQUE BaremeIRG(Code), BaremeIRGTranches(BaremeId, BorneInf), IRGReglesPeriode(Code) ;
///   - cas métier : barème 2008 (4 tranches), 3 règles de période 2020/2021/2022+,
///     paramètre d'arrondi (Q9), abattement 40 % [1000 ; 1500] et exonération 30 000.
/// </summary>
public class IrgParametresSchemaTests
{
    private static readonly string[] V006Tables =
    {
        "BaremeIRG", "BaremeIRGTranches", "IRGReglesPeriode", "Parametres"
    };

    // -----------------------------------------------------------------------
    // Tests structurels
    // -----------------------------------------------------------------------

    [Fact]
    public void V006_est_chargee_comme_ressource()
    {
        var names = typeof(SqliteMigrator).Assembly.GetManifestResourceNames();
        Assert.Contains($"{SchemaTestSupport.ResourcePrefix}V006__irg_parametres.sql", names);
    }

    [Fact]
    public void Apres_V006_les_4_tables_existent()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var existing = TableNames(scope.Conn);
        foreach (var t in V006Tables)
            Assert.Contains(t, existing);
    }

    [Fact]
    public void SchemaVersions_trace_V006()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var found = SchemaTestSupport.Scalar<long>(scope.Conn,
            "SELECT COUNT(*) FROM SchemaVersions WHERE Version = 6 AND Name = 'irg_parametres';");
        Assert.Equal(1L, found);
    }

    // -----------------------------------------------------------------------
    // CHECK & UNIQUE
    // -----------------------------------------------------------------------

    [Fact]
    public void CHECK_BaremeIRGTranches_Taux_borne_0_a_1()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertBareme(scope.Conn, "IRG-2008", "IRG-2008", "Barème 2008", "2007-01-01");
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO BaremeIRGTranches (Id, BaremeId, BorneInf, BorneSup, Taux, Ordre, CreatedAt, Hash) " +
            "VALUES ('T-1', 'IRG-2008', 0, 10000, 1.5, 1, '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CHECK_Parametres_Type_accepte_seulement_enum_5_valeurs()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO Parametres (Id, Cle, Valeur, Type, DateEffet, CreatedAt, Hash) " +
            "VALUES ('P-1', 'X', 'Y', 'JSON', '2007-01-01', '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UNIQUE_BaremeIRG_Code_pas_de_doublon_de_code()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertBareme(scope.Conn, "IRG-1", "IRG-2008", "Barème 2008", "2007-01-01");
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO BaremeIRG (Id, Code, Libelle, DateEffet, CreatedAt, Hash) " +
            "VALUES ('IRG-2', 'IRG-2008', 'Doublon', '2007-01-01', '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("UNIQUE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UNIQUE_BaremeIRGTranches_Bareme_BorneInf_pas_de_doublon()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertBareme(scope.Conn, "IRG-2008", "IRG-2008", "Barème 2008", "2007-01-01");
        InsertTranche(scope.Conn, "T-1", "IRG-2008", 0, 10000, 0.0, 1);
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO BaremeIRGTranches (Id, BaremeId, BorneInf, BorneSup, Taux, Ordre, CreatedAt, Hash) " +
            "VALUES ('T-DUP', 'IRG-2008', 0, 5000, 0.10, 2, '2026-01-01T00:00:00Z', 'h');"));
        Assert.Contains("UNIQUE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // Cas métier : barème 2008
    // -----------------------------------------------------------------------

    [Fact]
    public void Cas_metier_bareme_2008_a_4_tranches_comme_attendu()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertBareme(scope.Conn, "IRG-2008", "IRG-2008", "Barème 2008", "2007-01-01");
        InsertTranche(scope.Conn, "T-1", "IRG-2008", 0,      10000,   0.00, 1); // 0 %
        InsertTranche(scope.Conn, "T-2", "IRG-2008", 10001,  30000,   0.20, 2); // 20 %
        InsertTranche(scope.Conn, "T-3", "IRG-2008", 30001,  120000,  0.30, 3); // 30 %
        InsertTranche(scope.Conn, "T-4", "IRG-2008", 120001, null,    0.35, 4); // 35 %

        // Requête de résolution : pour un imposable donné, trouver la tranche.
        // L'imposable 25 000 tombe dans la tranche 10 001–30 000 (20 %).
        var tauxPour25k = SchemaTestSupport.Scalar<double>(scope.Conn,
            "SELECT Taux FROM BaremeIRGTranches " +
            "WHERE BaremeId = 'IRG-2008' AND BorneInf <= $i AND (BorneSup IS NULL OR BorneSup >= $i);",
            ("$i", 25000));
        Assert.Equal(0.20, tauxPour25k);

        // L'imposable 5 000 tombe dans la tranche 0–10 000 (0 %).
        var tauxPour5k = SchemaTestSupport.Scalar<double>(scope.Conn,
            "SELECT Taux FROM BaremeIRGTranches " +
            "WHERE BaremeId = 'IRG-2008' AND BorneInf <= $i AND (BorneSup IS NULL OR BorneSup >= $i);",
            ("$i", 5000));
        Assert.Equal(0.00, tauxPour5k);

        // L'imposable 200 000 tombe dans la tranche 120 001+ (35 %).
        var tauxPour200k = SchemaTestSupport.Scalar<double>(scope.Conn,
            "SELECT Taux FROM BaremeIRGTranches " +
            "WHERE BaremeId = 'IRG-2008' AND BorneInf <= $i AND (BorneSup IS NULL OR BorneSup >= $i);",
            ("$i", 200000));
        Assert.Equal(0.35, tauxPour200k);
    }

    // -----------------------------------------------------------------------
    // Cas métier : 3 règles de période (2020, 2021, 2022+)
    // -----------------------------------------------------------------------

    [Fact]
    public void Cas_metier_3_regles_de_periode_2020_2021_2022_plus_avec_exoneration_et_abattement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertBareme(scope.Conn, "IRG-2008", "IRG-2008", "Barème 2008", "2007-01-01");

        // Période 2020 : exonération 30 000, abattement 40 % [1000;1500], pas de lissage.
        InsertReglePeriode(scope.Conn, "PER-2020", "Période 2020", "2020-01-01", "2020-12-31",
            baremeId: "IRG-2008",
            exon: 30000, abattTaux: 0.40, abattMin: 1000, abattMax: 1500,
            coefGen: 1.0, constGen: 0, coefSpe: 1.0, constSpe: 0, plafondSpe: 0);

        // Période 2021 : mêmes valeurs que 2020.
        InsertReglePeriode(scope.Conn, "PER-2021", "Période 2021", "2021-01-01", "2021-12-31",
            baremeId: "IRG-2008",
            exon: 30000, abattTaux: 0.40, abattMin: 1000, abattMax: 1500,
            coefGen: 1.0, constGen: 0, coefSpe: 1.0, constSpe: 0, plafondSpe: 0);

        // Période 2022+ : lissage par coefficients (à calibrer contre les bulletins réels).
        // On pose un exemple conservateur : coefGeneral = 0.80, constGeneral = 0.
        // Les valeurs définitives viendront de la validation réglementaire (Phase 8).
        InsertReglePeriode(scope.Conn, "PER-2022+", "Période 2022+", "2022-01-01", null,
            baremeId: "IRG-2008",
            exon: 30000, abattTaux: 0.40, abattMin: 1000, abattMax: 1500,
            coefGen: 0.80, constGen: 0, coefSpe: 0.80, constSpe: 0, plafondSpe: 15000);

        // Vérifie l'exonération et l'abattement : on s'attend à 30 000 et 40 % pour toutes.
        var exon = SchemaTestSupport.Scalar<int>(scope.Conn,
            "SELECT ExonerationSeuil FROM IRGReglesPeriode WHERE Code = 'PER-2022+';");
        var abattTaux = SchemaTestSupport.Scalar<double>(scope.Conn,
            "SELECT AbattementTaux FROM IRGReglesPeriode WHERE Code = 'PER-2022+';");
        var abattMin = SchemaTestSupport.Scalar<int>(scope.Conn,
            "SELECT AbattementMin FROM IRGReglesPeriode WHERE Code = 'PER-2022+';");
        var abattMax = SchemaTestSupport.Scalar<int>(scope.Conn,
            "SELECT AbattementMax FROM IRGReglesPeriode WHERE Code = 'PER-2022+';");
        var coefG = SchemaTestSupport.Scalar<double>(scope.Conn,
            "SELECT CoefGeneral FROM IRGReglesPeriode WHERE Code = 'PER-2022+';");
        Assert.Equal(30000, exon);
        Assert.Equal(0.40, abattTaux);
        Assert.Equal(1000, abattMin);
        Assert.Equal(1500, abattMax);
        Assert.Equal(0.80, coefG);
    }

    [Fact]
    public void Resolution_IRGReglesPeriode_a_une_date_retourne_la_bonne_regle()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertBareme(scope.Conn, "IRG-2008", "IRG-2008", "Barème 2008", "2007-01-01");
        InsertReglePeriode(scope.Conn, "PER-2020", "Période 2020", "2020-01-01", "2020-12-31", "IRG-2008",
            30000, 0.40, 1000, 1500, 1.0, 0, 1.0, 0, 0);
        InsertReglePeriode(scope.Conn, "PER-2021", "Période 2021", "2021-01-01", "2021-12-31", "IRG-2008",
            30000, 0.40, 1000, 1500, 1.0, 0, 1.0, 0, 0);
        InsertReglePeriode(scope.Conn, "PER-2022+", "Période 2022+", "2022-01-01", null, "IRG-2008",
            30000, 0.40, 1000, 1500, 0.80, 0, 0.80, 0, 15000);

        Assert.Equal("PER-2020",  ResolveReglePeriode(scope.Conn, "2020-06-15"));
        Assert.Equal("PER-2020",  ResolveReglePeriode(scope.Conn, "2020-12-31"));
        Assert.Equal("PER-2021",  ResolveReglePeriode(scope.Conn, "2021-01-01"));
        Assert.Equal("PER-2021",  ResolveReglePeriode(scope.Conn, "2021-12-31"));
        Assert.Equal("PER-2022+", ResolveReglePeriode(scope.Conn, "2022-01-01"));
        Assert.Equal("PER-2022+", ResolveReglePeriode(scope.Conn, "2030-01-01"));
    }

    // -----------------------------------------------------------------------
    // Cas métier : Paramètres système
    // -----------------------------------------------------------------------

    [Fact]
    public void Cas_metier_parametre_ARRONDI_MODE_selon_Q9()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        // Q9b : défaut = DINAR_PLUS_PROCHE (paramétrable).
        SchemaTestSupport.Exec(scope.Conn,
            "INSERT INTO Parametres (Id, Cle, Valeur, Type, Description, DateEffet, CreatedAt, Hash) " +
            "VALUES ('P-ARR', 'ARRONDI_MODE', 'DINAR_PLUS_PROCHE', 'TEXT', " +
            "        'Mode d''arrondi par défaut (Q9b)', '2007-01-01', '2026-01-01T00:00:00Z', 'h');");

        var mode = SchemaTestSupport.Scalar<string>(scope.Conn,
            "SELECT Valeur FROM Parametres WHERE Cle = 'ARRONDI_MODE' AND DateEffet <= '2026-01-01' " +
            "  AND (DateFin IS NULL OR DateFin >= '2026-01-01');");
        Assert.Equal("DINAR_PLUS_PROCHE", mode);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static HashSet<string> TableNames(SqliteConnection conn)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) set.Add(reader.GetString(0));
        return set;
    }

    private static void InsertBareme(SqliteConnection c, string id, string code, string libelle, string dateEffet)
    {
        SchemaTestSupport.Exec(c,
            "INSERT INTO BaremeIRG (Id, Code, Libelle, DateEffet, CreatedAt, Hash) " +
            "VALUES ($id, $c, $l, $de, $t, $h);",
            ("$id", id), ("$c", code), ("$l", libelle), ("$de", dateEffet),
            ("$t", "2026-01-01T00:00:00Z"), ("$h", $"h-{id}"));
    }

    private static void InsertTranche(
        SqliteConnection c, string id, string baremeId,
        int borneInf, int? borneSup, double taux, int ordre)
    {
        SchemaTestSupport.Exec(c,
            "INSERT INTO BaremeIRGTranches (Id, BaremeId, BorneInf, BorneSup, Taux, Ordre, CreatedAt, Hash) " +
            "VALUES ($id, $b, $bi, $bs, $t, $o, $ts, $h);",
            ("$id", id), ("$b", baremeId), ("$bi", borneInf),
            ("$bs", (object?)borneSup ?? DBNull.Value),
            ("$t", taux), ("$o", ordre), ("$ts", "2026-01-01T00:00:00Z"), ("$h", $"h-{id}"));
    }

    private static void InsertReglePeriode(
        SqliteConnection c, string id, string libelle,
        string dateDebut, string? dateFin, string baremeId,
        int exon, double abattTaux, int abattMin, int abattMax,
        double coefGen, int constGen, double coefSpe, int constSpe, int plafondSpe)
    {
        SchemaTestSupport.Exec(c,
            "INSERT INTO IRGReglesPeriode (Id, Code, Libelle, DateDebut, DateFin, BaremeId, " +
            "  ExonerationSeuil, AbattementTaux, AbattementMin, AbattementMax, " +
            "  CoefGeneral, ConstGeneral, CoefSpecial, ConstSpecial, PlafondSpecial, " +
            "  CreatedAt, Hash) " +
            "VALUES ($id, $id, $l, $dd, $df, $b, $ex, $at, $amn, $amx, " +
            "  $cg, $cng, $cs, $cns, $ps, $ts, $h);",
            ("$id", id), ("$l", libelle), ("$dd", dateDebut),
            ("$df", (object?)dateFin ?? DBNull.Value), ("$b", baremeId),
            ("$ex", exon), ("$at", abattTaux), ("$amn", abattMin), ("$amx", abattMax),
            ("$cg", coefGen), ("$cng", constGen), ("$cs", coefSpe),
            ("$cns", constSpe), ("$ps", plafondSpe),
            ("$ts", "2026-01-01T00:00:00Z"), ("$h", $"h-{id}"));
    }

    private static string ResolveReglePeriode(SqliteConnection c, string date) =>
        SchemaTestSupport.Scalar<string>(c,
            "SELECT Code FROM IRGReglesPeriode " +
            "WHERE DateDebut <= $d AND (DateFin IS NULL OR DateFin >= $d) " +
            "ORDER BY DateDebut DESC LIMIT 1;",
            ("$d", date));
}
