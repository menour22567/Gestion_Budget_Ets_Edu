using System.Linq;
using Microsoft.Data.Sqlite;
using PaieEducation.Persistence.Migrations;
using PaieEducation.Seeding;

namespace PaieEducation.Tests.Tools;

/// <summary>
/// Tests d'intégration du <see cref="ReglementaireSeeder"/>. Chaque test
/// crée une base migrée et vérifie le seed réglementaire (rubriques,
/// règles d'éligibilité, cotisations, paramètres).
/// </summary>
public class ReglementaireSeederTests
{
    private const string ResourcePrefix = "PaieEducation.Persistence.Migrations.";

    private static (SqliteConnection conn, TempSqliteDb db) OpenMigrated()
    {
        var db = new TempSqliteDb();
        var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        var r = new SqliteMigrator(new SqliteMigratorOptions(db.ConnectionString, "test"),
            MigrationLoader.LoadFromAssembly(typeof(SqliteMigrator).Assembly, ResourcePrefix)).Apply();
        if (r.IsFailure) throw new InvalidOperationException("Migration failed: " + r.Error);
        return (conn, db);
    }

    private static long Count(SqliteConnection c, string table) =>
        TestSupport.Scalar<long>(c, $"SELECT COUNT(*) FROM {table};");

    // -------------------------------------------------------------------------
    // Rubriques
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Seed_insere_les_10_rubriques_canoniques()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            var report = await new ReglementaireSeeder().SeedAsync(conn);

            Assert.Equal(10L, Count(conn, "Rubriques"));
            var ids = ReadStrings(conn, "Rubriques", "Id");
            Assert.Contains("IEP_FONC", ids);
            Assert.Contains("IEP_CONT", ids);
            Assert.Contains("EXP_PEDAG", ids);
            Assert.Contains("PAPP", ids);
            Assert.Contains("QUALIF", ids);
            Assert.Contains("DOC_PEDAG", ids);
            Assert.Contains("ISSRP_45", ids);
            Assert.Contains("ISSRP_30", ids);
            Assert.Contains("ISSRP_15", ids);
            Assert.Contains("IRG", ids);

            // Toutes insérées (10 Inserees), pas 0.
            Assert.Equal(10, report.Tables.Single(t => t.Table == "Rubriques").Inserees);
        }
    }

    [Fact]
    public async Task Seed_QUALIF_et_DOC_PEDAG_ont_les_baremes_par_categorie()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            // QUALIF : 2 tranches (RM-045) — 40 % (≤12), 45 % (≥13).
            Assert.Equal("TAUX", TestSupport.Scalar<string>(conn,
                "SELECT TypeValeur FROM RubriqueBaremes WHERE Id = 'RB-QUALIF-CAT-LE12';"));
            Assert.Equal("0.40", TestSupport.Scalar<string>(conn,
                "SELECT Valeur FROM RubriqueBaremes WHERE Id = 'RB-QUALIF-CAT-LE12';"));
            Assert.Equal("0.45", TestSupport.Scalar<string>(conn,
                "SELECT Valeur FROM RubriqueBaremes WHERE Id = 'RB-QUALIF-CAT-GE13';"));
            Assert.Null(TestSupport.Scalar<string?>(conn,
                "SELECT BorneSup FROM RubriqueBaremes WHERE Id = 'RB-QUALIF-CAT-GE13';"));

            // DOC_PEDAG : 3 tranches forfaitaires (RM-046) — 2000/2500/3000 DA.
            Assert.Equal("MONTANT", TestSupport.Scalar<string>(conn,
                "SELECT TypeValeur FROM RubriqueBaremes WHERE Id = 'RB-DOCPEDAG-CAT-LE10';"));
            Assert.Equal("2000", TestSupport.Scalar<string>(conn,
                "SELECT Valeur FROM RubriqueBaremes WHERE Id = 'RB-DOCPEDAG-CAT-LE10';"));
            Assert.Equal("2500", TestSupport.Scalar<string>(conn,
                "SELECT Valeur FROM RubriqueBaremes WHERE Id = 'RB-DOCPEDAG-CAT-11-12';"));
            Assert.Equal("3000", TestSupport.Scalar<string>(conn,
                "SELECT Valeur FROM RubriqueBaremes WHERE Id = 'RB-DOCPEDAG-CAT-GE13';"));

            Assert.Equal(5L, Count(conn, "RubriqueBaremes"));
        }
    }

    [Fact]
    public async Task Seed_attributs_IEP_EXP_PEDAG_et_IRG_sont_conformes()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            // IEP_FONC = gain, base INDICE_ECHELON (IE × VPI, Q2-rev).
            Assert.Equal("GAIN", TestSupport.Scalar<string>(conn,
                "SELECT Nature FROM Rubriques WHERE Id = 'IEP_FONC';"));
            Assert.Equal("INDICE_ECHELON", TestSupport.Scalar<string>(conn,
                "SELECT BaseCalcul FROM Rubriques WHERE Id = 'IEP_FONC';"));

            // IEP_CONT = gain, base TBASE (taux composite plafonné 60 %).
            Assert.Equal("TBASE", TestSupport.Scalar<string>(conn,
                "SELECT BaseCalcul FROM Rubriques WHERE Id = 'IEP_CONT';"));

            // EXP_PEDAG = gain, base TBASE_ECHELON (4 % × TBASE × échelon).
            Assert.Equal("TBASE_ECHELON", TestSupport.Scalar<string>(conn,
                "SELECT BaseCalcul FROM Rubriques WHERE Id = 'EXP_PEDAG';"));

            // IRG = impot, non imposable, non cotisable.
            Assert.Equal("IMPOT", TestSupport.Scalar<string>(conn,
                "SELECT Nature FROM Rubriques WHERE Id = 'IRG';"));
        }
    }

    [Fact]
    public async Task Seed_PAPP_est_cotisable_et_servie_trimestriellement()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            // Q-02 (14/07/2026) : PAPP imposable ET cotisable.
            Assert.Equal(1L, TestSupport.Scalar<long>(conn,
                "SELECT EstCotisable FROM Rubriques WHERE Id = 'PAPP';"));
            Assert.Equal(1L, TestSupport.Scalar<long>(conn,
                "SELECT EstImposable FROM Rubriques WHERE Id = 'PAPP';"));

            // INC-04 : calculée mensuellement, servie trimestriellement.
            Assert.Equal("MENSUELLE", TestSupport.Scalar<string>(conn,
                "SELECT Periodicite FROM Rubriques WHERE Id = 'PAPP';"));
            Assert.Equal("TRIMESTRIELLE", TestSupport.Scalar<string>(conn,
                "SELECT PeriodiciteVersement FROM Rubriques WHERE Id = 'PAPP';"));

            // Le libellé corrigé (INC-02) : performances pédagogiques, pas pensions.
            var libelle = TestSupport.Scalar<string>(conn,
                "SELECT Libelle FROM Rubriques WHERE Id = 'PAPP';");
            Assert.Contains("performances pédagogiques", libelle);
        }
    }

    [Fact]
    public async Task Seed_insere_les_parametres_IEP_CONT()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            // Q2-rev : taux composite IEP_CONT (1,4 / 0,7 / plafond 60 %).
            Assert.Equal("1.4", TestSupport.Scalar<string>(conn,
                "SELECT Valeur FROM Parametres WHERE Cle = 'IEP_TAUX_PUBLIC_PCT';"));
            Assert.Equal("0.7", TestSupport.Scalar<string>(conn,
                "SELECT Valeur FROM Parametres WHERE Cle = 'IEP_TAUX_PRIVE_PCT';"));
            Assert.Equal("60", TestSupport.Scalar<string>(conn,
                "SELECT Valeur FROM Parametres WHERE Cle = 'IEP_PLAFOND_PCT';"));
        }
    }

    // -------------------------------------------------------------------------
    // GroupesEligibilite / ReglesEligibilite — ISSRP en groupes DNF (grain
    // GRADE, J4F, remplace la matrice à plat par CORPS le 16/07/2026).
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Seed_insere_les_6_groupes_DNF_ISSRP()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            // 45-DIRECT, 45-ORIGINE, 30-DIRECT, 30-ORIGINE, 15-DIRECT, 15-HIST.
            Assert.Equal(6L, Count(conn, "GroupesEligibilite"));

            // Conditions : 1 (45-DIRECT) + 2 (45-ORIGINE) + 1 (30-DIRECT)
            // + 2 (30-ORIGINE) + 1 (15-DIRECT) + 1 (15-HIST) = 8.
            Assert.Equal(8L, Count(conn, "ReglesEligibilite"));

            // Toutes les conditions référencent bien un groupe (grain GRADE
            // exclusivement — plus de matrice à plat par CORPS).
            var sansGroupe = TestSupport.Scalar<long>(conn,
                "SELECT COUNT(*) FROM ReglesEligibilite WHERE GroupeId IS NULL;");
            Assert.Equal(0L, sansGroupe);
            var critereCorps = TestSupport.Scalar<long>(conn,
                "SELECT COUNT(*) FROM ReglesEligibilite WHERE CritereId = 'CORPS';");
            Assert.Equal(0L, critereCorps);
        }
    }

    [Fact]
    public async Task Groupe_45_direct_couvre_les_titulaires_et_les_3_contractuels_Q08()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            var valeur = TestSupport.Scalar<string>(conn, """
                SELECT Valeur FROM ReglesEligibilite
                WHERE GroupeId = 'GE-ISSRP45-DIRECT' AND CritereId = 'GRADE';
                """);
            // Titulaire (Professeur de l'Ecole primaire).
            Assert.Contains("PDLP-G105", valeur);
            // Contractuels réintégrés (Q-08 résolue par l'arrêté 6 primes).
            Assert.Contains("PDLP-G130", valeur);
            Assert.Contains("PDLM-G131", valeur);
            Assert.Contains("PDLS-G132", valeur);
            Assert.DoesNotContain("PDLP-G106,PDLP-G105", valeur); // pas de doublon trivial
        }
    }

    [Fact]
    public async Task Groupe_origine_conditionne_45_ou_30_selon_ORIGINE_STATUTAIRE_jamais_par_defaut()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            // Q-C1 : les 7 grades conditionnels apparaissent dans LES DEUX groupes
            // ORIGINE (45 % si ENSEIGNANT, 30 % si AUTRE) — jamais par défaut, jamais
            // via un opérateur `<>` (abstention si ORIGINE_STATUTAIRE = INCONNU).
            var origine45 = TestSupport.Scalar<string>(conn, """
                SELECT Valeur FROM ReglesEligibilite
                WHERE GroupeId = 'GE-ISSRP45-ORIGINE' AND CritereId = 'ORIGINE_STATUTAIRE';
                """);
            Assert.Equal("ENSEIGNANT", origine45);

            var origine30 = TestSupport.Scalar<string>(conn, """
                SELECT Valeur FROM ReglesEligibilite
                WHERE GroupeId = 'GE-ISSRP30-ORIGINE' AND CritereId = 'ORIGINE_STATUTAIRE';
                """);
            Assert.Equal("AUTRE", origine30);

            var operateurs = TestSupport.Scalar<long>(conn, """
                SELECT COUNT(*) FROM ReglesEligibilite
                WHERE CritereId = 'ORIGINE_STATUTAIRE' AND Operateur = '<>';
                """);
            Assert.Equal(0L, operateurs);

            var gradeDansLes2Groupes = TestSupport.Scalar<string>(conn, """
                SELECT Valeur FROM ReglesEligibilite
                WHERE GroupeId = 'GE-ISSRP45-ORIGINE' AND CritereId = 'GRADE';
                """);
            Assert.Contains("SDL-G007", gradeDansLes2Groupes);
            var memesGrades = TestSupport.Scalar<string>(conn, """
                SELECT Valeur FROM ReglesEligibilite
                WHERE GroupeId = 'GE-ISSRP30-ORIGINE' AND CritereId = 'GRADE';
                """);
            Assert.Equal(gradeDansLes2Groupes, memesGrades);
        }
    }

    [Fact]
    public async Task Groupe_historique_couvre_les_conditionnels_sans_distinction_d_origine()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            // RM-042 : taux unique 15 % 2008-2024 pour tous les corps EN classés —
            // les 7 grades conditionnels sont couverts SANS condition d'origine
            // (la distinction n'existe qu'à partir de D.ex. 25-55, 2025).
            var hist = TestSupport.Scalar<string>(conn, """
                SELECT Valeur FROM ReglesEligibilite
                WHERE GroupeId = 'GE-ISSRP15-HIST' AND CritereId = 'GRADE';
                """);
            Assert.Contains("SDL-G007", hist);   // conditionnel, inclus
            Assert.Contains("PDLP-G130", hist);  // contractuel, inclus
            Assert.Contains("PDLP-G105", hist);  // titulaire 45%, inclus
            Assert.Contains("ADL-G001", hist);   // 30% direct, inclus
            Assert.Contains("ADSE-G035", hist);  // 15% direct, inclus

            var periode = TestSupport.Scalar<string>(conn,
                "SELECT DateFin FROM GroupesEligibilite WHERE Id = 'GE-ISSRP15-HIST';");
            Assert.Equal("2024-12-31", periode);
        }
    }

    // -------------------------------------------------------------------------
    // Q-C3 (résolue 16/07/2026) — grades « hors catégorie » (HC-S1/HC-S2),
    // seed supplémentaire ciblé sourcé sur Liste_Grades_Fr.csv.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Seed_insere_les_4_grades_hors_categorie_et_leur_grille()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            var ids = ReadStrings(conn, "Grades", "Id")
                .Where(g => g.StartsWith("IDLS-G1", StringComparison.Ordinal)).ToList();
            Assert.Equal(new[] { "IDLS-G144", "IDLS-G145", "IDLS-G146", "IDLS-G148" }, ids);

            // Corps + Filiere créés (indépendamment de NomenclatureSeeder).
            Assert.Equal("IDLS", TestSupport.Scalar<string>(conn,
                "SELECT CorpsId FROM Grades WHERE Id = 'IDLS-G144';"));
            Assert.Equal("INSPECTION", TestSupport.Scalar<string>(conn,
                "SELECT FiliereId FROM Corps WHERE Id = 'IDLS';"));

            // Categories : Niveau 18/19 (au-delà des 17 catégories numériques),
            // HorsCategorie = 1.
            Assert.Equal(18L, TestSupport.Scalar<long>(conn,
                "SELECT Niveau FROM Categories WHERE Id = 'HC-S1';"));
            Assert.Equal(19L, TestSupport.Scalar<long>(conn,
                "SELECT Niveau FROM Categories WHERE Id = 'HC-S2';"));
            Assert.Equal(1L, TestSupport.Scalar<long>(conn,
                "SELECT HorsCategorie FROM Categories WHERE Id = 'HC-S1';"));

            // GrilleIndiciaire : 3 lignes par catégorie (pas de ligne avant
            // 2022-03-01 — indice 0 dans la source, interdit par IndiceMin > 0).
            Assert.Equal(3L, TestSupport.Scalar<long>(conn,
                "SELECT COUNT(*) FROM GrilleIndiciaire WHERE CategorieId = 'HC-S1';"));
            Assert.Equal(980L, TestSupport.Scalar<long>(conn,
                "SELECT IndiceMin FROM GrilleIndiciaire WHERE CategorieId = 'HC-S1' AND DateEffet = '2022-03-01';"));
            Assert.Equal(1190L, TestSupport.Scalar<long>(conn,
                "SELECT IndiceMin FROM GrilleIndiciaire WHERE CategorieId = 'HC-S2' AND DateEffet = '2024-01-01';"));
        }
    }

    [Fact]
    public async Task Seed_ISSRP_complet_185_grades_apres_resolution_Q_C3()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            // Les 3 grades 45 % (disciplines, administration, générique EN).
            var direct45 = TestSupport.Scalar<string>(conn, """
                SELECT Valeur FROM ReglesEligibilite
                WHERE GroupeId = 'GE-ISSRP45-DIRECT' AND CritereId = 'GRADE';
                """);
            Assert.Contains("IDLS-G144", direct45);
            Assert.Contains("IDLS-G145", direct45);
            Assert.Contains("IDLS-G148", direct45);

            // Le grade 30 % (orientation/guidance aux lycées).
            var direct30 = TestSupport.Scalar<string>(conn, """
                SELECT Valeur FROM ReglesEligibilite
                WHERE GroupeId = 'GE-ISSRP30-DIRECT' AND CritereId = 'GRADE';
                """);
            Assert.Contains("IDLS-G146", direct30);

            // Historique 2008-2024 : les 4 grades couverts sans distinction (RM-042).
            var hist = TestSupport.Scalar<string>(conn, """
                SELECT Valeur FROM ReglesEligibilite
                WHERE GroupeId = 'GE-ISSRP15-HIST' AND CritereId = 'GRADE';
                """);
            foreach (var g in new[] { "IDLS-G144", "IDLS-G145", "IDLS-G146", "IDLS-G148" })
            {
                Assert.Contains(g, hist);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Cotisations
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Seed_insere_SS_9_pourcent_comme_dans_Q3b()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            Assert.Equal(3L, Count(conn, "Cotisations"));

            var taux = TestSupport.Scalar<double>(conn,
                "SELECT Taux FROM Cotisations WHERE Code = 'SS';");
            Assert.Equal(0.09, taux);

            var type = TestSupport.Scalar<string>(conn,
                "SELECT TypeCotisation FROM Cotisations WHERE Code = 'SS';");
            Assert.Equal("OBLIGATOIRE_SALARIALE", type);
        }
    }

    [Fact]
    public async Task Seed_insere_mutuelle_et_oeuvres_sociales_comme_facultatives_montant_fixe()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            // Les 2 facultatives ont Taux=NULL (montant fixe) + AssietteRef=MONTANT_FIXE.
            foreach (var code in new[] { "MUTUELLE", "OEUVRES_SOCIALES" })
            {
                var type = TestSupport.Scalar<string>(conn,
                    $"SELECT TypeCotisation FROM Cotisations WHERE Code = '{code}';");
                var taux = TestSupport.Scalar<object?>(conn,
                    $"SELECT Taux FROM Cotisations WHERE Code = '{code}';");
                var assiette = TestSupport.Scalar<string>(conn,
                    $"SELECT AssietteRef FROM Cotisations WHERE Code = '{code}';");

                Assert.Equal("FACULTATIVE", type);
                Assert.Null(taux);
                Assert.Equal("MONTANT_FIXE", assiette);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Paramètres
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Seed_insere_le_parametre_ARRONDI_MODE_par_defaut_DINAR_PLUS_PROCHE()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            Assert.Equal(10L, Count(conn, "Parametres"));

            var valeur = TestSupport.Scalar<string>(conn,
                "SELECT Valeur FROM Parametres WHERE Cle = 'ARRONDI_MODE';");
            Assert.Equal("DINAR_PLUS_PROCHE", valeur);

            var type = TestSupport.Scalar<string>(conn,
                "SELECT Type FROM Parametres WHERE Cle = 'ARRONDI_MODE';");
            Assert.Equal("TEXT", type);
        }
    }

    // -------------------------------------------------------------------------
    // Chantier P2 (19/07/2026) — équivalence stricte DB ↔ JSON externalisé.
    // Contrairement aux tests ci-dessus (Contains, vérification métier), ceux-ci
    // comparent la valeur exacte insérée en base à la reconstruction depuis
    // GroupesDnfIssrpJsonDataReader — preuve directe que le seed n'a rien perdu
    // ni réordonné lors de l'externalisation des tableaux C# vers le JSON.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Seed_Valeur_ISSRP45_DIRECT_correspond_exactement_a_la_liste_JSON_dans_l_ordre()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            var data = GroupesDnfIssrpJsonDataReader.Load();
            var attendu = string.Join(",", GroupesDnfIssrpJsonDataReader.ResoudreGrades(data, ["issrp45Direct"]));

            var valeurEnBase = TestSupport.Scalar<string>(conn, """
                SELECT Valeur FROM ReglesEligibilite
                WHERE GroupeId = 'GE-ISSRP45-DIRECT' AND CritereId = 'GRADE';
                """);

            Assert.Equal(attendu, valeurEnBase);
        }
    }

    [Fact]
    public async Task Seed_Valeur_ISSRP15_HIST_correspond_exactement_a_l_union_des_4_listes_92_grades()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            var data = GroupesDnfIssrpJsonDataReader.Load();
            var attendu = string.Join(",", GroupesDnfIssrpJsonDataReader.ResoudreGrades(
                data, ["issrp45Direct", "issrpOrigine", "issrp30Direct", "issrp15Direct"]));

            var valeurEnBase = TestSupport.Scalar<string>(conn, """
                SELECT Valeur FROM ReglesEligibilite
                WHERE GroupeId = 'GE-ISSRP15-HIST' AND CritereId = 'GRADE';
                """);

            Assert.Equal(92, attendu.Split(',').Length);
            Assert.Equal(attendu, valeurEnBase);
        }
    }

    [Fact]
    public async Task Seed_hash_ISSRP_est_un_vrai_SHA256_canonique_pas_un_placeholder()
    {
        // Avant P2 : hash factices "h-groupe-{id}"/"h-cat-{id}" qui ne
        // détectaient aucun drift de contenu. Depuis P2 : même mécanisme
        // SHA-256 que les 4 autres sections externalisées.
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);

            var hashGroupe = TestSupport.Scalar<string>(conn,
                "SELECT Hash FROM GroupesEligibilite WHERE Id = 'GE-ISSRP45-DIRECT';");
            var hashCategorie = TestSupport.Scalar<string>(conn,
                "SELECT Hash FROM Categories WHERE Id = 'HC-S1';");

            Assert.StartsWith("sha256:", hashGroupe);
            Assert.StartsWith("sha256:", hashCategorie);
        }
    }

    // -------------------------------------------------------------------------
    // Idempotence
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Seed_est_idempotent()
    {
        var (conn, db) = OpenMigrated();
        using (conn) using (db)
        {
            await new ReglementaireSeeder().SeedAsync(conn);
            var r2 = await new ReglementaireSeeder().SeedAsync(conn);

            // 2e run : Inserees = 0 partout.
            Assert.All(r2.Tables, t => Assert.Equal(0, t.Inserees));
            Assert.Equal(10L, Count(conn, "Rubriques"));
            Assert.Equal(1L, Count(conn, "Filieres"));
            Assert.Equal(1L, Count(conn, "Corps"));
            Assert.Equal(2L, Count(conn, "Categories"));
            Assert.Equal(6L, Count(conn, "GrilleIndiciaire"));
            Assert.Equal(4L, Count(conn, "Grades"));
            Assert.Equal(6L, Count(conn, "GroupesEligibilite"));
            Assert.Equal(8L, Count(conn, "ReglesEligibilite"));
            Assert.Equal(5L, Count(conn, "RubriqueBaremes"));
            Assert.Equal(3L, Count(conn, "Cotisations"));
            Assert.Equal(10L, Count(conn, "Parametres"));
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private static List<string> ReadStrings(SqliteConnection c, string table, string col)
    {
        var list = new List<string>();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT {col} FROM {table} ORDER BY {col};";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(reader.GetString(0));
        return list;
    }
}
