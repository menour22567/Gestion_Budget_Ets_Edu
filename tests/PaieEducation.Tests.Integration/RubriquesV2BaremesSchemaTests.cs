using Microsoft.Data.Sqlite;

namespace PaieEducation.Tests.Integration;

/// <summary>
/// Tests du schéma V008 (extensions J3 approuvées le 14/07/2026, cf.
/// <c>docs/analysis/J3E_MODELE_PARAMETRAGE.md</c>) :
/// PeriodiciteVersement, BaseCalcul INDICE_ECHELON, critères d'éligibilité
/// étendus (ORIGINE_CORPS, TYPE_ETABLISSEMENT), RubriqueBaremes, GradeAttributs.
/// </summary>
public class RubriquesV2BaremesSchemaTests
{
    private static void InsertRubrique(SqliteConnection c, string id,
        string baseCalcul = "TRAITEMENT", string? periodiciteVersement = null)
    {
        SchemaTestSupport.Exec(c, """
            INSERT INTO Rubriques
                (Id, Libelle, Nature, BaseCalcul, Periodicite, PeriodiciteVersement,
                 OrdreCalcul, CreatedAt, Hash)
            VALUES ($id, 'Test', 'GAIN', $b, 'MENSUELLE', $pv, 1, '2026-01-01T00:00:00Z', 'h');
            """,
            ("$id", id), ("$b", baseCalcul), ("$pv", periodiciteVersement));
    }

    // -------------------------------------------------------------------------
    // Rubriques : PeriodiciteVersement + INDICE_ECHELON
    // -------------------------------------------------------------------------
    [Fact]
    public void Rubriques_accepte_PeriodiciteVersement_TRIMESTRIELLE_et_NULL()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        // NULL = versement identique à la périodicité de calcul.
        InsertRubrique(scope.Conn, "R-NULL");
        // PAPP-like : calcul mensuel, versement trimestriel (INC-04).
        InsertRubrique(scope.Conn, "R-TRIM", periodiciteVersement: "TRIMESTRIELLE");

        Assert.Equal("TRIMESTRIELLE", SchemaTestSupport.Scalar<string>(scope.Conn,
            "SELECT PeriodiciteVersement FROM Rubriques WHERE Id = 'R-TRIM';"));
    }

    [Fact]
    public void CHECK_Rubriques_PeriodiciteVersement_rejette_valeur_inconnue()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var ex = Assert.Throws<SqliteException>(() =>
            InsertRubrique(scope.Conn, "R-BAD", periodiciteVersement: "HEBDOMADAIRE"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rubriques_accepte_BaseCalcul_INDICE_ECHELON_pour_IEP_FONC()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        // Q2-rev : IEP_FONC = IE × VPI, assiette = indice d'échelon.
        InsertRubrique(scope.Conn, "R-IEP", baseCalcul: "INDICE_ECHELON");
        Assert.Equal(1L, SchemaTestSupport.Scalar<long>(scope.Conn,
            "SELECT COUNT(*) FROM Rubriques WHERE BaseCalcul = 'INDICE_ECHELON';"));
    }

    // -------------------------------------------------------------------------
    // ReglesEligibilite : critères étendus
    // -------------------------------------------------------------------------
    [Fact]
    public void ReglesEligibilite_accepte_ORIGINE_STATUTAIRE_et_TYPE_ETABLISSEMENT()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertRubrique(scope.Conn, "ISSRP_45X");

        // R3 (V009) : Critere TEXT+CHECK supprimé, remplacé par CritereId FK vers
        // le dictionnaire CriteresEligibilite. Le critère "origine" est désormais
        // porté par `ORIGINE_STATUTAIRE` (D3) et `TYPE_ETABLISSEMENT`.
        // Q-03 (J3B) : « grades de promotion d'origine enseignante » → ORIGINE_STATUTAIRE.
        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO ReglesEligibilite
                (Id, RubriqueId, CritereId, Operateur, Valeur, DateEffet, Hash, CreatedAt)
            VALUES ('RE-1', 'ISSRP_45X', 'ORIGINE_STATUTAIRE', '=', 'ENSEIGNANT',
                    '2025-01-01', 'h', '2026-01-01T00:00:00Z');
            """);
        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO ReglesEligibilite
                (Id, RubriqueId, CritereId, Operateur, Valeur, DateEffet, Hash, CreatedAt)
            VALUES ('RE-2', 'ISSRP_45X', 'TYPE_ETABLISSEMENT', 'IN', 'PRIMAIRE,COLLEGE,LYCEE',
                    '2015-09-01', 'h', '2026-01-01T00:00:00Z');
            """);

        Assert.Equal(2L, SchemaTestSupport.Scalar<long>(scope.Conn,
            "SELECT COUNT(*) FROM ReglesEligibilite;"));
    }

    // -------------------------------------------------------------------------
    // RubriqueBaremes : barèmes par tranche de critère
    // -------------------------------------------------------------------------
    [Fact]
    public void RubriqueBaremes_stocke_un_bareme_IFC_par_categorie()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertRubrique(scope.Conn, "IFC", baseCalcul: "FORFAIT");

        // Extrait du barème IFC 2015 (D.ex. 15-176) : cat 7–8 → 3 800 DA.
        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO RubriqueBaremes
                (Id, RubriqueId, Dimension, BorneInf, BorneSup, TypeValeur, Valeur,
                 DateEffet, Hash, CreatedAt)
            VALUES ('RB-IFC-2015-7-8', 'IFC', 'CATEGORIE', '7', '8', 'MONTANT', '3800',
                    '2015-01-01', 'h', '2026-01-01T00:00:00Z');
            """);

        Assert.Equal("3800", SchemaTestSupport.Scalar<string>(scope.Conn,
            "SELECT Valeur FROM RubriqueBaremes WHERE RubriqueId = 'IFC';"));
    }

    [Fact]
    public void CHECK_RubriqueBaremes_rejette_Dimension_inconnue()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertRubrique(scope.Conn, "IFC", baseCalcul: "FORFAIT");

        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO RubriqueBaremes
                (Id, RubriqueId, Dimension, BorneInf, TypeValeur, Valeur,
                 DateEffet, Hash, CreatedAt)
            VALUES ('RB-X', 'IFC', 'COULEUR', '1', 'MONTANT', '1',
                    '2015-01-01', 'h', '2026-01-01T00:00:00Z');
            """));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UNIQUE_RubriqueBaremes_rejette_le_doublon_de_tranche_a_meme_date()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsertRubrique(scope.Conn, "IFC", baseCalcul: "FORFAIT");

        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO RubriqueBaremes
                (Id, RubriqueId, Dimension, BorneInf, TypeValeur, Valeur, DateEffet, Hash, CreatedAt)
            VALUES ('RB-1', 'IFC', 'CATEGORIE', '1', 'MONTANT', '7700', '2015-01-01', 'h', '2026-01-01T00:00:00Z');
            """);
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO RubriqueBaremes
                (Id, RubriqueId, Dimension, BorneInf, TypeValeur, Valeur, DateEffet, Hash, CreatedAt)
            VALUES ('RB-2', 'IFC', 'CATEGORIE', '1', 'MONTANT', '9999', '2015-01-01', 'h', '2026-01-01T00:00:00Z');
            """));
        Assert.Contains("UNIQUE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // GradeAttributs
    // -------------------------------------------------------------------------
    [Fact]
    public void GradeAttributs_reference_un_grade_existant()
    {
        using var scope = SchemaTestSupport.CreateMigrated();

        // Grades exige une nomenclature minimale (FK en cascade).
        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO Filieres (Id, Libelle, CreatedAt, Hash)
            VALUES ('ENSEIGNANT', 'Enseignant', '2026-01-01T00:00:00Z', 'h');
            """);
        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash)
            VALUES ('C1', 'Corps test', 'ENSEIGNANT', '2026-01-01T00:00:00Z', 'h');
            """);
        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash)
            VALUES ('G1', 'Grade test', 'C1', 1, '2026-01-01T00:00:00Z', 'h');
            """);

        SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO GradeAttributs (Id, GradeId, Attribut, Valeur, DateEffet, Hash, CreatedAt)
            VALUES ('GA-1', 'G1', 'ORIGINE_ENSEIGNANTE_POSSIBLE', '1', '2025-01-01', 'h', '2026-01-01T00:00:00Z');
            """);

        Assert.Equal("1", SchemaTestSupport.Scalar<string>(scope.Conn,
            "SELECT Valeur FROM GradeAttributs WHERE GradeId = 'G1';"));

        // FK : un grade inexistant est rejeté.
        var ex = Assert.Throws<SqliteException>(() => SchemaTestSupport.Exec(scope.Conn, """
            INSERT INTO GradeAttributs (Id, GradeId, Attribut, Valeur, DateEffet, Hash, CreatedAt)
            VALUES ('GA-2', 'INCONNU', 'X', '1', '2025-01-01', 'h', '2026-01-01T00:00:00Z');
            """));
        Assert.Contains("FOREIGN KEY", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
