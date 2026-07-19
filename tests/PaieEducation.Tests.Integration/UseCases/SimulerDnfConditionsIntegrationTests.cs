using Microsoft.Data.Sqlite;
using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Infrastructure.Repositories.Agents;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Infrastructure.Repositories.Workbench;
using PaieEducation.Seeding;
using PaieEducation.Shared.Time;

namespace PaieEducation.Tests.Integration.UseCases;

/// <summary>
/// Preuve d'intégration que le <strong>chemin full</strong> du simulateur
/// d'impact réel (D8 / ADR-0007) respecte correctement les conditions
/// d'éligibilité DNF (D5) — conditions regroupées (OU de groupes, ET dans
/// groupe). Lot 3.4 (J5N §3). Aucune modification de code de production :
/// ces tests verrouillent un invariant déjà couvert par le simulateur
/// mais non explicitement testé bout-en-bout.
/// </summary>
public class SimulerDnfConditionsIntegrationTests
{
    private sealed class HorlogeFixe(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
        public DateTimeOffset UtcNow => now;
        public DateOnly Today => DateOnly.FromDateTime(now.UtcDateTime);
    }

    private static readonly HorlogeFixe Horloge = new(new DateTimeOffset(2026, 7, 19, 10, 0, 0, TimeSpan.Zero));

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
    /// Seed 4 agents pilotes aux caractéristiques distinctes pour tester
    /// l'éligibilité DNF :
    /// <list type="bullet">
    ///   <item>A-PEM : corps PEM, cat 13, ech 5, ENSEIGNANT</item>
    ///   <item>A-CENSEUR : corps CENSEUR, cat 13, ech 5, ENSEIGNANT</item>
    ///   <item>A-PELP : corps PELP, cat 13, ech 5, ENSEIGNANT</item>
    ///   <item>A-AUTRE : corps PEM, cat 13, ech 5, AUTRE (pas ENSEIGNANT)</item>
    /// </list>
    /// </summary>
    private static void SeedAgentsDnf(SqliteConnection conn)
    {
        Exec(conn, """
            INSERT INTO Filieres (Id, Libelle, CreatedAt, Hash) VALUES ('ENSEIGNANT', 'Enseignant', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ('PEM', 'Prof. École moyenne', 'ENSEIGNANT', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ('CENSEUR', 'Censeur', 'ENSEIGNANT', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ('PELP', 'Prof. École primaire', 'ENSEIGNANT', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('PEM-G1', 'Grade PEM', 'PEM', 1, '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('CENSEUR-G1', 'Grade Censeur', 'CENSEUR', 1, '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('PELP-G1', 'Grade PELP', 'PELP', 1, '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('13', 13, 'Catégorie 13', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('5', 5, 'Échelon 5', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO ValeurPoint (Id, DateEffet, Valeur, Version, Hash, CreatedAt) VALUES ('VP-PILOTE', '2007-01-01', 45, 'v', 'h', '2026-01-01T00:00:00Z');
            INSERT INTO GrilleIndiciaire (Id, CategorieId, DateEffet, IndiceMin, Version, Hash, CreatedAt) VALUES ('GI-PILOTE', '13', '2020-01-01', 578, 'v', 'h', '2026-01-01T00:00:00Z');
            INSERT INTO IndicesEchelon (Id, EchelonId, DateEffet, Indice, Version, Hash, CreatedAt) VALUES ('IE-PILOTE', '5', '2020-01-01', 100, 'v', 'h', '2026-01-01T00:00:00Z');
            INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, CreatedAt)
                VALUES ('A-PEM', 'MAT-PEM', 'PEM', 'Test', '1985-01-01', '2010-09-01', 'M', '2026-01-01T00:00:00Z');
            INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, CreatedAt)
                VALUES ('A-CENSEUR', 'MAT-CENS', 'CENSEUR', 'Test', '1985-01-01', '2010-09-01', 'M', '2026-01-01T00:00:00Z');
            INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, CreatedAt)
                VALUES ('A-PELP', 'MAT-PELP', 'PELP', 'Test', '1985-01-01', '2010-09-01', 'M', '2026-01-01T00:00:00Z');
            INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, CreatedAt)
                VALUES ('A-AUTRE', 'MAT-AUTR', 'AUTRE', 'Test', '1985-01-01', '2010-09-01', 'M', '2026-01-01T00:00:00Z');
            INSERT INTO Carrieres (Id, AgentId, GradeId, CategorieId, EchelonId, TypeContrat, DateEffet, Motif, CreatedAt)
                VALUES ('C-PEM', 'A-PEM', 'PEM-G1', '13', '5', 'STATUTAIRE', '2010-09-01', 'Recrutement', '2026-01-01T00:00:00Z');
            INSERT INTO Carrieres (Id, AgentId, GradeId, CategorieId, EchelonId, TypeContrat, DateEffet, Motif, CreatedAt)
                VALUES ('C-CENS', 'A-CENSEUR', 'CENSEUR-G1', '13', '5', 'STATUTAIRE', '2010-09-01', 'Recrutement', '2026-01-01T00:00:00Z');
            INSERT INTO Carrieres (Id, AgentId, GradeId, CategorieId, EchelonId, TypeContrat, DateEffet, Motif, CreatedAt)
                VALUES ('C-PELP', 'A-PELP', 'PELP-G1', '13', '5', 'STATUTAIRE', '2010-09-01', 'Recrutement', '2026-01-01T00:00:00Z');
            INSERT INTO Carrieres (Id, AgentId, GradeId, CategorieId, EchelonId, TypeContrat, DateEffet, Motif, CreatedAt)
                VALUES ('C-AUTR', 'A-AUTRE', 'PEM-G1', '13', '5', 'STATUTAIRE', '2010-09-01', 'Recrutement', '2026-01-01T00:00:00Z');
            INSERT INTO AgentAttributs (Id, AgentId, Attribut, Valeur, DateEffet, CreatedAt)
                VALUES ('AA-PEM', 'A-PEM', 'ORIGINE_STATUTAIRE', 'ENSEIGNANT', '2010-09-01', '2026-01-01T00:00:00Z');
            INSERT INTO AgentAttributs (Id, AgentId, Attribut, Valeur, DateEffet, CreatedAt)
                VALUES ('AA-CENS', 'A-CENSEUR', 'ORIGINE_STATUTAIRE', 'ENSEIGNANT', '2010-09-01', '2026-01-01T00:00:00Z');
            INSERT INTO AgentAttributs (Id, AgentId, Attribut, Valeur, DateEffet, CreatedAt)
                VALUES ('AA-PELP', 'A-PELP', 'ORIGINE_STATUTAIRE', 'ENSEIGNANT', '2010-09-01', '2026-01-01T00:00:00Z');
            INSERT INTO AgentAttributs (Id, AgentId, Attribut, Valeur, DateEffet, CreatedAt)
                VALUES ('AA-AUTR', 'A-AUTRE', 'ORIGINE_STATUTAIRE', 'AUTRE', '2010-09-01', '2026-01-01T00:00:00Z');
            """);
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
    public async Task DNF_a_2_groupes_OU_compte_tous_les_agents_eligibles_via_l_un_OU_l_autre()
    {
        // C-D1 : règle ISSRP_45 DNF = (CORPS = PEM) OU (CORPS = CENSEUR ET ORIGINE = ENSEIGNANT).
        // Attendus : A-PEM (groupe A satisfait, CORPS=PEM), A-AUTRE (groupe A
        // satisfait aussi, CORPS=PEM peu importe l'origine), A-CENSEUR
        // (groupe B satisfait : CORPS=CENSEUR ET ORIGINE=ENSEIGNANT),
        // A-PELP (aucun groupe ne satisfait). → NbAgents = 3.
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentsDnf(scope.Conn);
        var (simuler, _) = BuildUseCases(scope.Conn);

        var periode = PeriodeReglementaire.Creer("2026-01-01", null);
        var conditions = new[]
        {
            ConditionEligibilite.Creer("C-A1", "ISSRP_45", "CORPS",
                Operateur.Egal, "PEM", "GA", periode),
            ConditionEligibilite.Creer("C-B1", "ISSRP_45", "CORPS",
                Operateur.Egal, "CENSEUR", "GB", periode),
            ConditionEligibilite.Creer("C-B2", "ISSRP_45", "ORIGINE_STATUTAIRE",
                Operateur.Egal, "ENSEIGNANT", "GB", periode),
        };
        var criteres = new Dictionary<string, CritereEligibilite>
        {
            ["CORPS"] = CritereEligibilite.Creer("CORPS", "Corps",
                TypeValeurCritere.Enum, SourceResolution.Carriere),
            ["ORIGINE_STATUTAIRE"] = CritereEligibilite.Creer("ORIGINE_STATUTAIRE",
                "Origine statutaire", TypeValeurCritere.Enum, SourceResolution.AttributAgent),
        };

        var demande = new SimulerEvolutionReglementaire.Demande(
            RubriqueId: "ISSRP_45",
            Description: "DNF 2 groupes",
            NouvellePeriode: periode,
            PeriodesExistantes: Array.Empty<PeriodeReglementaire>(),
            ConditionsApres: conditions,
            Criteres: criteres,
            NouvelleValeurPoint: 50m,   // <-- déclenche le chemin full
            AgentIdsPourImpact: new[] { "A-PEM", "A-CENSEUR", "A-PELP", "A-AUTRE" },
            DateCalcul: "2026-01-01");

        var r = simuler.Executer(demande);

        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        Assert.Equal(3, r.Value.NbAgents);  // A-PEM, A-AUTRE, A-CENSEUR ; A-PELP non éligible
        Assert.True(r.Value.DeltaMinMensuel > 0m, $"DeltaMin doit être > 0 (reçu {r.Value.DeltaMinMensuel})");
        Assert.Equal(r.Value.DeltaMinMensuel, r.Value.DeltaMaxMensuel);
        Assert.Equal(r.Value.MontantTotalMensuel, r.Value.DeltaMinMensuel * 3m);  // 3 agents
    }

    [Fact]
    public async Task DNF_ET_dans_groupe_compte_une_seule_fois_les_agents_qui_satisfont_toutes_les_conditions()
    {
        // C-D2 : variante ET-dans-groupe. A-PEM satisfait les 2 conditions
        // (CORPS=PEM ET ORIGINE=ENSEIGNANT). A-AUTRE satisfait CORPS=PEM
        // mais pas ENSEIGNANT. → seul A-PEM est éligible (1 agent).
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentsDnf(scope.Conn);
        var (simuler, _) = BuildUseCases(scope.Conn);

        var periode = PeriodeReglementaire.Creer("2026-01-01", null);
        var conditions = new[]
        {
            ConditionEligibilite.Creer("C-A1", "ISSRP_45", "CORPS",
                Operateur.Egal, "PEM", null, periode),
            ConditionEligibilite.Creer("C-A2", "ISSRP_45", "ORIGINE_STATUTAIRE",
                Operateur.Egal, "ENSEIGNANT", null, periode),
        };
        var criteres = new Dictionary<string, CritereEligibilite>
        {
            ["CORPS"] = CritereEligibilite.Creer("CORPS", "Corps",
                TypeValeurCritere.Enum, SourceResolution.Carriere),
            ["ORIGINE_STATUTAIRE"] = CritereEligibilite.Creer("ORIGINE_STATUTAIRE",
                "Origine statutaire", TypeValeurCritere.Enum, SourceResolution.AttributAgent),
        };

        var demande = new SimulerEvolutionReglementaire.Demande(
            RubriqueId: "ISSRP_45",
            Description: "ET dans un groupe (sans OU)",
            NouvellePeriode: periode,
            PeriodesExistantes: Array.Empty<PeriodeReglementaire>(),
            ConditionsApres: conditions,
            Criteres: criteres,
            NouvelleValeurPoint: 50m,
            AgentIdsPourImpact: new[] { "A-PEM", "A-CENSEUR", "A-PELP", "A-AUTRE" },
            DateCalcul: "2026-01-01");

        var r = simuler.Executer(demande);

        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        Assert.Equal(1, r.Value.NbAgents);  // A-PEM seulement
        Assert.Equal(r.Value.DeltaMinMensuel, r.Value.MontantTotalMensuel);
    }

    [Fact]
    public async Task DNF_coherence_lite_et_full_path_donnent_le_meme_NbAgents()
    {
        // C-D3 : la même Demande exécutée en lite (parameterless) et en full
        // (deps injectées) doit retourner le même NbAgents.
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentsDnf(scope.Conn);
        var (simulerFull, _) = BuildUseCases(scope.Conn);

        var periode = PeriodeReglementaire.Creer("2026-01-01", null);
        var conditions = new[]
        {
            ConditionEligibilite.Creer("C-A1", "ISSRP_45", "CORPS",
                Operateur.Egal, "PEM", "GA", periode),
            ConditionEligibilite.Creer("C-B1", "ISSRP_45", "CORPS",
                Operateur.Egal, "CENSEUR", "GB", periode),
        };
        var criteres = new Dictionary<string, CritereEligibilite>
        {
            ["CORPS"] = CritereEligibilite.Creer("CORPS", "Corps",
                TypeValeurCritere.Enum, SourceResolution.Carriere),
        };

        var agentsLite = new List<AgentContext>
        {
            new("ENSEIGNANT", "PEM", null, 13, 5, 10, null, "STATUTAIRE", null, "ENSEIGNANT", 0.35m, 45m, null, null),
            new("ENSEIGNANT", "CENSEUR", null, 13, 5, 10, null, "STATUTAIRE", null, "ENSEIGNANT", 0.35m, 45m, null, null),
            new("ENSEIGNANT", "PELP", null, 13, 5, 10, null, "STATUTAIRE", null, "ENSEIGNANT", 0.35m, 45m, null, null),
        };
        var demandeLite = new SimulerEvolutionReglementaire.Demande(
            RubriqueId: "ISSRP_45",
            Description: "Lite vs full",
            NouvellePeriode: periode,
            PeriodesExistantes: Array.Empty<PeriodeReglementaire>(),
            AgentsCandidats: agentsLite,
            ConditionsApres: conditions,
            Criteres: criteres);

        var demandeFull = new SimulerEvolutionReglementaire.Demande(
            RubriqueId: "ISSRP_45",
            Description: "Lite vs full",
            NouvellePeriode: periode,
            PeriodesExistantes: Array.Empty<PeriodeReglementaire>(),
            ConditionsApres: conditions,
            Criteres: criteres,
            NouvelleValeurPoint: 50m,
            AgentIdsPourImpact: new[] { "A-PEM", "A-CENSEUR", "A-PELP" },
            DateCalcul: "2026-01-01");

        var rLite = new SimulerEvolutionReglementaire().Executer(demandeLite);
        var rFull = simulerFull.Executer(demandeFull);

        Assert.True(rLite.IsSuccess);
        Assert.True(rFull.IsSuccess);
        Assert.Equal(rLite.Value.NbAgents, rFull.Value.NbAgents);
        Assert.Equal(2, rLite.Value.NbAgents);  // PEM (A) + CENSEUR (B)
        Assert.Equal(2, rFull.Value.NbAgents);
    }

    [Fact]
    public async Task DNF_retroactif_avec_bulletins_valides_compte_BulletinsAvertis_sur_les_eligibles()
    {
        // C-D4 : DNF + rétroactivité. On valide 2 bulletins (A-PEM et
        // A-CENSEUR, les 2 éligibles). L'évolution au 2025-01-01 est
        // rétroactive (DateCalcul = 2026-07-19). BulletinsAvertis = 2.
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentsDnf(scope.Conn);
        var (simuler, valider) = BuildUseCases(scope.Conn);

        await ValiderBulletinPour(scope.Conn, valider, "A-PEM", "2025-03-01");
        await ValiderBulletinPour(scope.Conn, valider, "A-CENSEUR", "2025-04-01");

        var periode = PeriodeReglementaire.Creer("2025-01-01", null);
        var conditions = new[]
        {
            ConditionEligibilite.Creer("C-A1", "ISSRP_45", "CORPS",
                Operateur.Egal, "PEM", "GA", periode),
            ConditionEligibilite.Creer("C-B1", "ISSRP_45", "CORPS",
                Operateur.Egal, "CENSEUR", "GB", periode),
        };
        var criteres = new Dictionary<string, CritereEligibilite>
        {
            ["CORPS"] = CritereEligibilite.Creer("CORPS", "Corps",
                TypeValeurCritere.Enum, SourceResolution.Carriere),
        };

        var demande = new SimulerEvolutionReglementaire.Demande(
            RubriqueId: "ISSRP_45",
            Description: "DNF rétroactif",
            NouvellePeriode: periode,
            PeriodesExistantes: Array.Empty<PeriodeReglementaire>(),
            ConditionsApres: conditions,
            Criteres: criteres,
            NouvelleValeurPoint: 50m,
            AgentIdsPourImpact: new[] { "A-PEM", "A-CENSEUR", "A-PELP" },
            DateCalcul: "2026-07-19");

        var r = simuler.Executer(demande);

        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        Assert.Equal(2, r.Value.NbAgents);
        Assert.Equal(2, r.Value.BulletinsAvertis);
    }

    [Fact]
    public async Task DNF_avec_override_bareme_le_delta_est_calcule_seulement_sur_les_eligibles()
    {
        // C-D5 : combine DNF (3.4) + override barème (3.2). Le delta ne
        // doit être calculé que sur les agents éligibles (PEM + CENSEUR).
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentsDnf(scope.Conn);
        var (simuler, _) = BuildUseCases(scope.Conn);

        var baremeOverride = new[]
        {
            BaremeValue.Creer(
                rubriqueId: "DOC_PEDAG",
                dimension: BaremeDimension.Categorie,
                borneInf: "13", borneSup: null,
                typeValeur: BaremeTypeValeur.Montant,
                valeur: "4000",
                periode: PeriodeReglementaire.Creer("2026-01-01", null))
        };

        var periode = PeriodeReglementaire.Creer("2026-01-01", null);
        var conditions = new[]
        {
            ConditionEligibilite.Creer("C-A1", "DOC_PEDAG", "CORPS",
                Operateur.Egal, "PEM", "GA", periode),
            ConditionEligibilite.Creer("C-B1", "DOC_PEDAG", "CORPS",
                Operateur.Egal, "CENSEUR", "GB", periode),
        };
        var criteres = new Dictionary<string, CritereEligibilite>
        {
            ["CORPS"] = CritereEligibilite.Creer("CORPS", "Corps",
                TypeValeurCritere.Enum, SourceResolution.Carriere),
        };

        var demande = new SimulerEvolutionReglementaire.Demande(
            RubriqueId: "DOC_PEDAG",
            Description: "DNF + override barème",
            NouvellePeriode: periode,
            PeriodesExistantes: Array.Empty<PeriodeReglementaire>(),
            ConditionsApres: conditions,
            Criteres: criteres,
            NouvelleValeurPoint: null,
            AgentIdsPourImpact: new[] { "A-PEM", "A-CENSEUR", "A-PELP" },
            DateCalcul: "2026-01-01",
            BaremesOverride: baremeOverride);

        var r = simuler.Executer(demande);

        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        Assert.Equal(2, r.Value.NbAgents);
        Assert.True(r.Value.DeltaMinMensuel > 0m);
        Assert.Equal(r.Value.DeltaMinMensuel, r.Value.DeltaMaxMensuel);
        Assert.Equal(r.Value.DeltaMinMensuel * 2m, r.Value.MontantTotalMensuel);
    }

    private static async Task ValiderBulletinPour(SqliteConnection conn, ValiderBulletin valider, string agentId, string datePaie)
    {
        var demande = new CalculerBulletin.Demande(
            AgentId: agentId, DatePaie: datePaie,
            SourcesValeur: new Dictionary<string, decimal> { ["PAPP"] = 0.30m },
            ClesBareme: new Dictionary<string, string> { ["CATEGORIE"] = "13" },
            Profil: ProfilFiscal.Standard);
        var r = await valider.ExecuterAsync(demande);
        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
    }
}
