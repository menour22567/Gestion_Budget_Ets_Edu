using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Infrastructure.Repositories.Workbench;
using PaieEducation.Persistence.Migrations;

namespace PaieEducation.Tests.Integration.Workbench;

/// <summary>
/// Test d'intégration end-to-end du Workbench réglementaire :
///   1. Migration V001..V009 sur une base éphémère.
///   2. Insertion directe de données réglementaires (rubrique + barème + conditions + groupe DNF).
///   3. Lecture via <see cref="WorkbenchReadRepository"/> (Dapper).
///   4. Évaluation via <see cref="RegleEligibiliteEvaluator"/> (Domain).
///   5. Vérification du flux complet (catalogue → repo → evaluator).
///
/// C'est le test « smoke » de la Phase 3bis : il prouve que les couches
/// Domain, Application, Infrastructure et Persistence sont correctement
/// branchées bout-en-bout.
/// </summary>
public class WorkbenchIntegrationTests
{
    private const string ResourcePrefix = "PaieEducation.Persistence.Migrations.";

    private static (SqliteConnection Conn, SqliteMigrator Migrator) CreateMigrated()
    {
        var db = new TempSqliteDb();
        var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        var migrator = new SqliteMigrator(
            new SqliteMigratorOptions(db.ConnectionString, "test"),
            MigrationLoader.LoadFromAssembly(typeof(SqliteMigrator).Assembly, ResourcePrefix));
        var result = migrator.Apply();
        if (result.IsFailure) throw new InvalidOperationException("Migration failed: " + result.Error);
        return (conn, migrator);
    }

    private static AgentContext Agent(string corps, string? origine = "ENSEIGNANT",
        int? categorie = 7, int? echelon = 5, int? anciennete = 10)
        => new(Filiere: "ENSEIGNANT", Corps: corps, Grade: null, Categorie: categorie,
               Echelon: echelon, AncienneteAnnees: anciennete, Fonction: null,
               TypeContrat: "STATUTAIRE", TypeEtablissement: null,
               OrigineStatutaire: origine, Note: 0.35m, ValeurPointIndiciaire: 45m,
               AssietteCotisable: null, AssietteImposable: null);

    [Fact]
    public async Task EndToEnd_DNF_ISSRP_45_via_repo_et_evaluator()
    {
        // 1. Base migrée.
        var (conn, _) = CreateMigrated();
        await using (conn)
        {
            // 2. Schéma Workbench + données de test.
            // 2.1. Rubrique ISSRP_45
            Exec(conn, """
                INSERT INTO Rubriques
                    (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul, CreatedAt, Hash)
                VALUES ('ISSRP_45', 'Soutien scolaire 45 %', 'GAIN', 'TRAITEMENT',
                        'MENSUELLE', 1, '2026-01-01T00:00:00Z', 'h');
                """);

            // 2.2. Critère ORIGINE_STATUTAIRE (déjà dans le seed V009, on l'utilise).

            // 2.3. Groupe DNF (R5) : (GRADE direct) OU (CENSEUR ET ENSEIGNANT)
            Exec(conn, """
                INSERT INTO GroupesEligibilite
                    (Id, RubriqueId, Severite, MessageId, Priorite, DateEffet, Source, Hash, CreatedAt, CreatedBy)
                VALUES ('G-PEDAGO', 'ISSRP_45', 'OBLIGATOIRE_REGLEMENTAIRE', NULL, 100,
                        '2025-01-01', 'D.ex. 25-55 art. 10', 'h', '2026-01-01T00:00:00Z', 'system');
                """);

            Exec(conn, """
                INSERT INTO GroupesEligibilite
                    (Id, RubriqueId, Severite, MessageId, Priorite, DateEffet, Source, Hash, CreatedAt, CreatedBy)
                VALUES ('G-PROMO', 'ISSRP_45', 'OBLIGATOIRE_REGLEMENTAIRE', NULL, 50,
                        '2025-01-01', 'D.ex. 25-55 art. 10', 'h', '2026-01-01T00:00:00Z', 'system');
                """);

            // 2.4. Conditions : PEM → G-PEDAGO ; CENSEUR + ENSEIGNANT → G-PROMO
            Exec(conn, """
                INSERT INTO ReglesEligibilite
                    (Id, RubriqueId, CritereId, GroupeId, Operateur, Valeur, DateEffet, Hash, CreatedAt)
                VALUES ('C1', 'ISSRP_45', 'CORPS', 'G-PEDAGO', '=', 'PEM',
                        '2025-01-01', 'h', '2026-01-01T00:00:00Z');
                """);
            Exec(conn, """
                INSERT INTO ReglesEligibilite
                    (Id, RubriqueId, CritereId, GroupeId, Operateur, Valeur, DateEffet, Hash, CreatedAt)
                VALUES ('C2', 'ISSRP_45', 'CORPS', 'G-PROMO', '=', 'CENSEUR',
                        '2025-01-01', 'h', '2026-01-01T00:00:00Z');
                """);
            Exec(conn, """
                INSERT INTO ReglesEligibilite
                    (Id, RubriqueId, CritereId, GroupeId, Operateur, Valeur, DateEffet, Hash, CreatedAt)
                VALUES ('C3', 'ISSRP_45', 'ORIGINE_STATUTAIRE', 'G-PROMO', '=', 'ENSEIGNANT',
                        '2025-01-01', 'h', '2026-01-01T00:00:00Z');
                """);

            // 3. Lecture via le repository.
            var repo = new WorkbenchReadRepository(conn);
            var groupes = await repo.ListerGroupesParRubriqueAsync("ISSRP_45", "2025-06-15");
            var conditions = await repo.ListerConditionsParRubriqueAsync("ISSRP_45", "2025-06-15");
            var criteres = await repo.ListerCriteresParIdAsync();

            Assert.Equal(2, groupes.Count);
            Assert.Equal(3, conditions.Count);
            Assert.True(criteres.ContainsKey("CORPS"));
            Assert.True(criteres.ContainsKey("ORIGINE_STATUTAIRE"));

            // 4. Évaluation via l'evaluator du Domain.
            var eval = new RegleEligibiliteEvaluator(new CritereEligibiliteResolver());

            // Cas 1 : agent en PEM → G-PEDAGO satisfait
            var r1 = eval.Evaluer("ISSRP_45", Agent("PEM"), "2025-06-15", conditions, criteres, groupes);
            Assert.True(r1.EstEligible);

            // Cas 2 : agent en CENSEUR + ENSEIGNANT → G-PROMO satisfait
            var r2 = eval.Evaluer("ISSRP_45", Agent("CENSEUR", "ENSEIGNANT"), "2025-06-15",
                conditions, criteres, groupes);
            Assert.True(r2.EstEligible);

            // Cas 3 : agent en CENSEUR + AUTRE → aucun groupe satisfait
            var r3 = eval.Evaluer("ISSRP_45", Agent("CENSEUR", "AUTRE"), "2025-06-15",
                conditions, criteres, groupes);
            Assert.False(r3.EstEligible);
        }
    }

    [Fact]
    public async Task Repo_charge_bareme_IFC_et_resolver_le_trouve()
    {
        // Démontre la chaîne complète : SQL → repo → BaremeValue → BaremeResolver.
        var (conn, _) = CreateMigrated();
        await using (conn)
        {
            Exec(conn, """
                INSERT INTO Rubriques
                    (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul, CreatedAt, Hash)
                VALUES ('IFC', 'Indemnité forfaitaire compensatrice', 'GAIN', 'FORFAIT',
                        'MENSUELLE', 1, '2026-01-01T00:00:00Z', 'h');
                """);
            Exec(conn, """
                INSERT INTO RubriqueBaremes
                    (Id, RubriqueId, Dimension, BorneInf, BorneSup, TypeValeur, Valeur,
                     DateEffet, Hash, CreatedAt)
                VALUES ('RB-IFC-7-8', 'IFC', 'CATEGORIE', '7', '8', 'MONTANT', '2500',
                        '2025-01-01', 'h', '2026-01-01T00:00:00Z');
                """);
            Exec(conn, """
                INSERT INTO RubriqueBaremes
                    (Id, RubriqueId, Dimension, BorneInf, BorneSup, TypeValeur, Valeur,
                     DateEffet, Hash, CreatedAt)
                VALUES ('RB-IFC-1-6', 'IFC', 'CATEGORIE', '1', '6', 'MONTANT', '3200',
                        '2025-01-01', 'h', '2026-01-01T00:00:00Z');
                """);

            var repo = new WorkbenchReadRepository(conn);
            var baremes = await repo.ListerBaremesRubriqueAsync("IFC");
            Assert.Equal(2, baremes.Count);

            var resolver = new BaremeResolver();
            var cat7 = resolver.Resoudre("IFC", BaremeDimension.Categorie, "7", "2025-06-15", baremes);
            var cat3 = resolver.Resoudre("IFC", BaremeDimension.Categorie, "3", "2025-06-15", baremes);
            var cat12 = resolver.Resoudre("IFC", BaremeDimension.Categorie, "12", "2025-06-15", baremes);

            Assert.Equal("2500", cat7!.Valeur);
            Assert.Equal("3200", cat3!.Valeur);
            Assert.Null(cat12);   // hors plage 1-8
        }
    }

    [Fact]
    public async Task Repo_liste_periodes_pour_continuite_temporelle()
    {
        // Vérifie que listerPeriodesRubriqueAsync produit une séquence de
        // PeriodesReglementaire utilisable par ContinuiteTemporelle.Valider.
        var (conn, _) = CreateMigrated();
        await using (conn)
        {
            Exec(conn, """
                INSERT INTO Rubriques
                    (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul, CreatedAt, Hash)
                VALUES ('DOC', 'Documentation', 'GAIN', 'FORFAIT', 'MENSUELLE', 1,
                        '2026-01-01T00:00:00Z', 'h');
                """);
            Exec(conn, """
                INSERT INTO RubriqueBaremes
                    (Id, RubriqueId, Dimension, BorneInf, BorneSup, TypeValeur, Valeur,
                     DateEffet, DateFin, Hash, CreatedAt)
                VALUES ('RB-1', 'DOC', 'CATEGORIE', '1', '10', 'MONTANT', '2000',
                        '2020-01-01', '2024-12-31', 'h', '2026-01-01T00:00:00Z');
                """);
            Exec(conn, """
                INSERT INTO RubriqueBaremes
                    (Id, RubriqueId, Dimension, BorneInf, BorneSup, TypeValeur, Valeur,
                     DateEffet, Hash, CreatedAt)
                VALUES ('RB-2', 'DOC', 'CATEGORIE', '1', '10', 'MONTANT', '2500',
                        '2025-01-01', 'h', '2026-01-01T00:00:00Z');
                """);

            var repo = new WorkbenchReadRepository(conn);
            var periodes = await repo.ListerPeriodesRubriqueAsync("DOC");
            Assert.Equal(2, periodes.Count);
            Assert.True(periodes[0].DateEffet == "2020-01-01");
            Assert.Equal("2024-12-31", periodes[0].DateFin);
            Assert.True(periodes[1].DateEffet == "2025-01-01");
            Assert.Null(periodes[1].DateFin);
        }
    }

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
