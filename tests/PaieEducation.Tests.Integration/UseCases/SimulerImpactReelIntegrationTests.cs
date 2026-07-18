using Microsoft.Data.Sqlite;
using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Infrastructure.Repositories.Agents;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Infrastructure.Repositories.Workbench;
using PaieEducation.Seeding;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Preuve d'intégration de <see cref="SimulerEvolutionReglementaire"/> (D8 /
/// ADR-0007) sur le chemin « impact réel » (J5L §3.3). Bout-en-bout jusqu'à
/// la lecture des <c>Bulletins</c> et la résolution des variables depuis la
/// base — prouve que la VPI hypothétique se propage correctement à travers
/// le pipeline (CalculerBulletin + VpiOverride) et que le delta est réel.
/// </summary>
public class SimulerImpactReelIntegrationTests
{
    private sealed class HorlogeFixe(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
        public DateTimeOffset UtcNow => now;
        public DateOnly Today => DateOnly.FromDateTime(now.UtcDateTime);
    }

    private static readonly HorlogeFixe Horloge = new(new DateTimeOffset(2026, 7, 18, 10, 0, 0, TimeSpan.Zero));

    private static async Task SeedTout(SqliteConnection conn)
    {
        await new ReglementaireSeeder().SeedAsync(conn);
        await new IrgSeeder().SeedAsync(conn);
        await new FormulesSeeder().SeedAsync(conn);
    }

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Seed l'agent pilote <c>A-PILOTE</c> (catégorie 13, échelon 5, ORIGINE_STATUTAIRE=ENSEIGNANT),
    /// valeur du point = 45 DA en 2007 (toujours en vigueur à 2025-06-01).
    /// </summary>
    private static void SeedAgentPilote(SqliteConnection conn)
    {
        Exec(conn, """
            INSERT INTO Filieres (Id, Libelle, CreatedAt, Hash) VALUES ('ENSEIGNANT', 'Enseignant', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ('PDLP', 'Prof. École primaire', 'ENSEIGNANT', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('PDLP-G105', 'Professeur de l''École primaire', 'PDLP', 1, '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('13', 13, 'Catégorie 13', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('5', 5, 'Échelon 5', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO ValeurPoint (Id, DateEffet, Valeur, Version, Hash, CreatedAt) VALUES ('VP-PILOTE', '2007-01-01', 45, 'v', 'h', '2026-01-01T00:00:00Z');
            INSERT INTO GrilleIndiciaire (Id, CategorieId, DateEffet, IndiceMin, Version, Hash, CreatedAt) VALUES ('GI-PILOTE', '13', '2020-01-01', 578, 'v', 'h', '2026-01-01T00:00:00Z');
            INSERT INTO IndicesEchelon (Id, EchelonId, DateEffet, Indice, Version, Hash, CreatedAt) VALUES ('IE-PILOTE', '5', '2020-01-01', 100, 'v', 'h', '2026-01-01T00:00:00Z');
            INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, CreatedAt)
                VALUES ('A-PILOTE', 'MAT-PILOTE', 'Test', 'Pilote', '1985-01-01', '2010-09-01', 'M', '2026-01-01T00:00:00Z');
            INSERT INTO Carrieres (Id, AgentId, GradeId, CategorieId, EchelonId, TypeContrat, DateEffet, Motif, CreatedAt)
                VALUES ('C-PILOTE', 'A-PILOTE', 'PDLP-G105', '13', '5', 'STATUTAIRE', '2010-09-01', 'Recrutement', '2026-01-01T00:00:00Z');
            INSERT INTO AgentAttributs (Id, AgentId, Attribut, Valeur, DateEffet, CreatedAt)
                VALUES ('AA-PILOTE', 'A-PILOTE', 'ORIGINE_STATUTAIRE', 'ENSEIGNANT', '2010-09-01', '2026-01-01T00:00:00Z');
            """);
    }

    /// <summary>
    /// Valide un bulletin pour <c>A-PILOTE</c> à une date de paie donnée
    /// (utilisé pour peupler la table <c>Bulletins</c> en vue du décompte
    /// <see cref="RapportImpact.BulletinsAvertis"/>).
    /// </summary>
    private static async Task ValiderBulletinPour(SqliteConnection conn, string datePaie, ValiderBulletin valider)
    {
        var demande = new CalculerBulletin.Demande(
            AgentId: "A-PILOTE", DatePaie: datePaie,
            SourcesValeur: new Dictionary<string, decimal> { ["PAPP"] = 0.30m },
            ClesBareme: new Dictionary<string, string> { ["CATEGORIE"] = "13" },
            Profil: ProfilFiscal.Standard);
        var r = await valider.ExecuterAsync(demande);
        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
    }

    private static (SimulerEvolutionReglementaire UseCase, ValiderBulletin Valider) BuildUseCases(SqliteConnection conn)
    {
        var agents = new AgentCarriereRepository(conn);
        var variables = new VariableRepository(conn);
        var payroll = new PayrollReadRepository(conn);
        var parametres = new ParametreSystemeRepository(conn);
        var bulletinsEcriture = new BulletinRepository(conn);
        var bulletinsLecture = new BulletinReadRepository(conn);
        var audit = new AuditLogRepository(conn);
        var grille = new GrilleIndiciaireRepository(conn);
        var calculer = new CalculerBulletin(agents, variables, payroll, parametres, SourceValeurResolverFactory.ResolverReel(conn));
        var valider = new ValiderBulletin(calculer, bulletinsEcriture, Horloge);
        var simuler = new SimulerEvolutionReglementaire(calculer, agents, bulletinsLecture, Horloge);
        return (simuler, valider);
    }

    [Fact]
    public async Task Executer_chemin_full_calcule_delta_positif_reel_entre_VPI_actuelle_et_hypothetique()
    {
        // C-S1 : DeltaMinMensuel, DeltaMaxMensuel, MontantTotalMensuel ne sont
        // plus jamais à 0 quand NouvelleValeurPoint est fourni et qu'il y a
        // au moins un agent éligible. Ici 1 agent éligible, VPI 45→50 :
        // augmentation de 5 DA du point, appliquée à (INDICE_MIN + INDICE_ECH)
        // = (578 + 100) = 678 → delta TRT = 5 * 678 = 3 390 DA brut. Le delta
        // NET (après IRG/cotisations) est strictement inférieur au brut.
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentPilote(scope.Conn);
        var (simuler, _) = BuildUseCases(scope.Conn);

        // La condition « corps = PDLP » rend l'agent éligible à la rubrique
        // représentative « VALEUR_POINT » (un simple critère sur le corps, peu
        // importe la sémantique — le but est de prouver qu'un agent éligible
        // produit un delta non nul).
        var conditions = new[]
        {
            ConditionEligibilite.Creer("C-IMP", "VALEUR_POINT", "CORPS",
                Operateur.Egal, "PDLP", null,
                PeriodeReglementaire.Creer("2026-01-01", null))
        };
        var criteres = new Dictionary<string, CritereEligibilite>
        {
            ["CORPS"] = CritereEligibilite.Creer("CORPS", "Corps",
                TypeValeurCritere.Enum, SourceResolution.Carriere)
        };

        var demande = new SimulerEvolutionReglementaire.Demande(
            RubriqueId: "VALEUR_POINT",
            Description: "Revalorisation VPI 45 → 50",
            NouvellePeriode: PeriodeReglementaire.Creer("2026-01-01", null),
            PeriodesExistantes: Array.Empty<PeriodeReglementaire>(),
            ConditionsApres: conditions,
            Criteres: criteres,
            NouvelleValeurPoint: 50m,
            AgentIdsPourImpact: new[] { "A-PILOTE" },
            DateCalcul: "2026-01-01");

        var r = simuler.Executer(demande);

        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        Assert.Equal(1, r.Value.NbAgents);
        Assert.True(r.Value.DeltaMinMensuel > 0m, $"DeltaMin doit être > 0 (reçu {r.Value.DeltaMinMensuel})");
        Assert.Equal(r.Value.DeltaMinMensuel, r.Value.DeltaMaxMensuel);   // 1 seul agent → min = max
        Assert.Equal(r.Value.DeltaMinMensuel, r.Value.MontantTotalMensuel);  // 1 seul agent → total = min
        Assert.Equal("2026-01-01", r.Value.PeriodeImpactee);
        Assert.Equal(0, r.Value.BulletinsAvertis);  // évolution future (DateEffet = DateCalcul) → pas rétroactive
    }

    [Fact]
    public async Task Executer_chemin_full_calcule_delta_negatif_quand_VPI_diminue()
    {
        // C-S1bis : delta négatif (VPI 50→45, baisse). Min = max = total < 0.
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentPilote(scope.Conn);
        var (simuler, _) = BuildUseCases(scope.Conn);

        var conditions = new[]
        {
            ConditionEligibilite.Creer("C-IMP", "VALEUR_POINT", "CORPS",
                Operateur.Egal, "PDLP", null,
                PeriodeReglementaire.Creer("2026-01-01", null))
        };
        var criteres = new Dictionary<string, CritereEligibilite>
        {
            ["CORPS"] = CritereEligibilite.Creer("CORPS", "Corps",
                TypeValeurCritere.Enum, SourceResolution.Carriere)
        };

        var demande = new SimulerEvolutionReglementaire.Demande(
            RubriqueId: "VALEUR_POINT",
            Description: "Baisse VPI 50 → 45",
            NouvellePeriode: PeriodeReglementaire.Creer("2026-01-01", null),
            PeriodesExistantes: Array.Empty<PeriodeReglementaire>(),
            ConditionsApres: conditions,
            Criteres: criteres,
            NouvelleValeurPoint: 45m,  // identique à l'actuelle → delta = 0, et NON négatif
            AgentIdsPourImpact: new[] { "A-PILOTE" },
            DateCalcul: "2026-01-01");

        // Cas 1 : VPI identique (45 → 45) → tous les deltas sont à 0 (le use
        // case ignore les deltas nuls — voir ExecuterCheminFull).
        var r1 = simuler.Executer(demande);
        Assert.True(r1.IsSuccess);
        Assert.Equal(0m, r1.Value.DeltaMinMensuel);
        Assert.Equal(0m, r1.Value.MontantTotalMensuel);

        // Cas 2 : pour obtenir un delta < 0 il faut VPI < VPI actuelle. Comme
        // l'agent a VPI=45 en base, fixons NouvelleValeurPoint=40. Le TRT
        // baisse de 5 * 678 = 3 390 DA, le NET baisse d'autant.
        var demandeBaisse = demande with { NouvelleValeurPoint = 40m };
        var r2 = simuler.Executer(demandeBaisse);
        Assert.True(r2.IsSuccess, r2.IsFailure ? r2.Error.Message : null);
        Assert.True(r2.Value.DeltaMinMensuel < 0m, $"DeltaMin doit être < 0 (reçu {r2.Value.DeltaMinMensuel})");
        Assert.Equal(r2.Value.DeltaMinMensuel, r2.Value.MontantTotalMensuel);
    }

    [Fact]
    public async Task Executer_chemin_full_retroactif_compte_les_bulletins_valides_dans_la_periode()
    {
        // C-S2 : BulletinsAvertis = nombre de bulletins validés dans la
        // période [DateEffet, today[ pour une évolution rétroactive.
        // On valide 2 bulletins en 2025-03-01 et 2025-04-01, puis on simule
        // une évolution rétroactive au 2025-01-01.
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentPilote(scope.Conn);
        var (simuler, valider) = BuildUseCases(scope.Conn);

        // Validation de 2 bulletins (l'un après l'autre — unicité AgentId+DatePaie).
        await ValiderBulletinPour(scope.Conn, "2025-03-01", valider);
        await ValiderBulletinPour(scope.Conn, "2025-04-01", valider);

        var conditions = new[]
        {
            ConditionEligibilite.Creer("C-IMP", "VALEUR_POINT", "CORPS",
                Operateur.Egal, "PDLP", null,
                PeriodeReglementaire.Creer("2025-01-01", null))
        };
        var criteres = new Dictionary<string, CritereEligibilite>
        {
            ["CORPS"] = CritereEligibilite.Creer("CORPS", "Corps",
                TypeValeurCritere.Enum, SourceResolution.Carriere)
        };

        var demande = new SimulerEvolutionReglementaire.Demande(
            RubriqueId: "VALEUR_POINT",
            Description: "Revalorisation rétroactive 01/2025",
            NouvellePeriode: PeriodeReglementaire.Creer("2025-01-01", null),
            PeriodesExistantes: Array.Empty<PeriodeReglementaire>(),
            ConditionsApres: conditions,
            Criteres: criteres,
            NouvelleValeurPoint: 50m,
            AgentIdsPourImpact: new[] { "A-PILOTE" },
            // DateCalcul = aujourd'hui (2026-07-18) > DateEffet (2025-01-01) → rétroactif
            DateCalcul: "2026-07-18");

        var r = simuler.Executer(demande);

        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        Assert.Equal(1, r.Value.NbAgents);
        Assert.Equal(2, r.Value.BulletinsAvertis);  // les 2 bulletins 2025-03 et 2025-04 sont dans la période rétroactive
    }

    [Fact]
    public async Task Executer_chemin_full_evolution_future_BulletinsAvertis_est_zero()
    {
        // C-S3 : évolution future (DateEffet > DateCalcul) → BulletinsAvertis = 0.
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentPilote(scope.Conn);
        var (simuler, valider) = BuildUseCases(scope.Conn);

        // 1 bulletin validé en 2025-06 (dans le passé), mais l'évolution
        // simulée est au 2027-01-01 (dans le futur par rapport à aujourd'hui
        // 2026-07-18) → pas rétroactive.
        await ValiderBulletinPour(scope.Conn, "2025-06-01", valider);

        var conditions = new[]
        {
            ConditionEligibilite.Creer("C-IMP", "VALEUR_POINT", "CORPS",
                Operateur.Egal, "PDLP", null,
                PeriodeReglementaire.Creer("2027-01-01", null))
        };
        var criteres = new Dictionary<string, CritereEligibilite>
        {
            ["CORPS"] = CritereEligibilite.Creer("CORPS", "Corps",
                TypeValeurCritere.Enum, SourceResolution.Carriere)
        };

        var demande = new SimulerEvolutionReglementaire.Demande(
            RubriqueId: "VALEUR_POINT",
            Description: "Revalorisation future 2027",
            NouvellePeriode: PeriodeReglementaire.Creer("2027-01-01", null),
            PeriodesExistantes: Array.Empty<PeriodeReglementaire>(),
            ConditionsApres: conditions,
            Criteres: criteres,
            NouvelleValeurPoint: 50m,
            AgentIdsPourImpact: new[] { "A-PILOTE" },
            DateCalcul: "2026-07-18");

        var r = simuler.Executer(demande);

        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        Assert.Equal(0, r.Value.BulletinsAvertis);
    }

    [Fact]
    public async Task Executer_chemin_full_idempotent_deux_appels_successifs_produisent_le_meme_rapport()
    {
        // C-S5 : le calcul est idempotent. 2 simulations successives avec les
        // mêmes paramètres produisent le même RapportImpact.
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentPilote(scope.Conn);
        var (simuler, _) = BuildUseCases(scope.Conn);

        var conditions = new[]
        {
            ConditionEligibilite.Creer("C-IMP", "VALEUR_POINT", "CORPS",
                Operateur.Egal, "PDLP", null,
                PeriodeReglementaire.Creer("2026-01-01", null))
        };
        var criteres = new Dictionary<string, CritereEligibilite>
        {
            ["CORPS"] = CritereEligibilite.Creer("CORPS", "Corps",
                TypeValeurCritere.Enum, SourceResolution.Carriere)
        };

        var demande = new SimulerEvolutionReglementaire.Demande(
            RubriqueId: "VALEUR_POINT",
            Description: "Idempotence",
            NouvellePeriode: PeriodeReglementaire.Creer("2026-01-01", null),
            PeriodesExistantes: Array.Empty<PeriodeReglementaire>(),
            ConditionsApres: conditions,
            Criteres: criteres,
            NouvelleValeurPoint: 50m,
            AgentIdsPourImpact: new[] { "A-PILOTE" },
            DateCalcul: "2026-01-01");

        var r1 = simuler.Executer(demande);
        var r2 = simuler.Executer(demande);

        Assert.True(r1.IsSuccess);
        Assert.True(r2.IsSuccess);
        Assert.Equal(r1.Value.NbAgents, r2.Value.NbAgents);
        Assert.Equal(r1.Value.DeltaMinMensuel, r2.Value.DeltaMinMensuel);
        Assert.Equal(r1.Value.DeltaMaxMensuel, r2.Value.DeltaMaxMensuel);
        Assert.Equal(r1.Value.MontantTotalMensuel, r2.Value.MontantTotalMensuel);
        Assert.Equal(r1.Value.BulletinsAvertis, r2.Value.BulletinsAvertis);
    }

    [Fact]
    public async Task Executer_chemin_full_agent_non_eligible_exclu_du_delta_mais_compte_dans_NbAgents_zero()
    {
        // L'agent PDLP n'est PAS éligible (condition exige PELP) → NbAgents
        // reste à 0 et les deltas restent à 0.
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentPilote(scope.Conn);
        var (simuler, _) = BuildUseCases(scope.Conn);

        var conditions = new[]
        {
            ConditionEligibilite.Creer("C-IMP", "VALEUR_POINT", "CORPS",
                Operateur.Egal, "PELP", null,  // ← corps différent de l'agent (PDLP)
                PeriodeReglementaire.Creer("2026-01-01", null))
        };
        var criteres = new Dictionary<string, CritereEligibilite>
        {
            ["CORPS"] = CritereEligibilite.Creer("CORPS", "Corps",
                TypeValeurCritere.Enum, SourceResolution.Carriere)
        };

        var demande = new SimulerEvolutionReglementaire.Demande(
            RubriqueId: "VALEUR_POINT",
            Description: "Aucun agent éligible",
            NouvellePeriode: PeriodeReglementaire.Creer("2026-01-01", null),
            PeriodesExistantes: Array.Empty<PeriodeReglementaire>(),
            ConditionsApres: conditions,
            Criteres: criteres,
            NouvelleValeurPoint: 50m,
            AgentIdsPourImpact: new[] { "A-PILOTE" },
            DateCalcul: "2026-01-01");

        var r = simuler.Executer(demande);

        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        Assert.Equal(0, r.Value.NbAgents);
        Assert.Equal(0m, r.Value.DeltaMinMensuel);
        Assert.Equal(0m, r.Value.DeltaMaxMensuel);
        Assert.Equal(0m, r.Value.MontantTotalMensuel);
    }

    [Fact]
    public async Task Executer_chemin_full_validation_de_continuite_temporelle_meme_avec_NouvelleValeurPoint()
    {
        // C-S6 (régression) : la validation L-U8 (pas de chevauchement) doit
        // s'appliquer AVANT tout I/O. Sans cela, une évolution invalide
        // pourrait être simulée puis committée.
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentPilote(scope.Conn);
        var (simuler, _) = BuildUseCases(scope.Conn);

        var conditions = new[]
        {
            ConditionEligibilite.Creer("C-IMP", "VALEUR_POINT", "CORPS",
                Operateur.Egal, "PDLP", null,
                PeriodeReglementaire.Creer("2011-06-01", null))
        };
        var criteres = new Dictionary<string, CritereEligibilite>
        {
            ["CORPS"] = CritereEligibilite.Creer("CORPS", "Corps",
                TypeValeurCritere.Enum, SourceResolution.Carriere)
        };

        var demande = new SimulerEvolutionReglementaire.Demande(
            RubriqueId: "VALEUR_POINT",
            Description: "Évolution qui chevauche",
            NouvellePeriode: PeriodeReglementaire.Creer("2011-06-01", null),
            PeriodesExistantes: new[]
            {
                PeriodeReglementaire.Creer("2008-01-01", "2010-12-31"),
                PeriodeReglementaire.Creer("2011-01-01", "2014-12-31")  // chevauche
            },
            ConditionsApres: conditions,
            Criteres: criteres,
            NouvelleValeurPoint: 50m,
            AgentIdsPourImpact: new[] { "A-PILOTE" },
            DateCalcul: "2011-06-01");

        var r = simuler.Executer(demande);

        Assert.True(r.IsFailure);
        Assert.Contains("continuité", r.Error.Message);
    }
}
