using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Infrastructure.Repositories.Agents;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Tools.Seeding;

namespace PaieEducation.Tests.Integration.Calcul;

/// <summary>
/// Jalon J4 — preuve de bout en bout : migration V001→V010 → seed réglementaire
/// (ReglementaireSeeder + IrgSeeder) → seed des <b>formules</b> (FormulesSeeder,
/// expressions en base) → <see cref="PayrollReadRepository"/> (lecture) →
/// <see cref="CalculationPipeline"/> (calcul) → bulletin enseignant complet.
///
/// Le net (57 739 DA) est identique au test unitaire du pipeline (contexte en
/// mémoire) : la même formule, lue en base, produit le même résultat — c'est la
/// démonstration que rien n'est codé en dur. L'éligibilité ISSRP est résolue au
/// grain <b>GRADE</b> via les groupes DNF réels du seeder (J4F, 16/07/2026) —
/// plus aucun contournement local : c'était la dette explicitement signalée par
/// ce test depuis J4.c (« remodéliser la matrice ISSRP en groupes DNF »).
/// </summary>
public class BulletinEndToEndTests
{
    // Variables de base du pilote (résolues de la grille en Phase 5 ; fournies ici).
    private static readonly Dictionary<string, decimal> VariablesBase = new()
    {
        ["INDICE_MIN"] = 578m, ["INDICE_ECH"] = 100m, ["VPI"] = 45m,
        ["TBASE"] = 26010m, ["TRT"] = 30510m, ["ECH"] = 5m, ["CAT"] = 13m,
    };

    private static AgentContext Enseignant(string grade) => new(
        Filiere: "ENSEIGNANT", Corps: null, Grade: grade, Categorie: 13, Echelon: 5,
        AncienneteAnnees: 10, Fonction: null, TypeContrat: "STATUTAIRE",
        TypeEtablissement: null, OrigineStatutaire: "ENSEIGNANT",
        Note: 0.30m, ValeurPointIndiciaire: 45m, AssietteCotisable: null, AssietteImposable: null);

    private static async Task SeedTout(SqliteConnection conn)
    {
        await new ReglementaireSeeder().SeedAsync(conn);
        await new IrgSeeder().SeedAsync(conn);
        await new FormulesSeeder().SeedAsync(conn);
    }

    private static async Task<PaieEducation.Domain.Common.Result<PayrollInput>> Charger(
        PayrollReadRepository repo, string grade)
        => await repo.ChargerAsync(
            Enseignant(grade), "2025-06-01", VariablesBase,
            new Dictionary<string, decimal> { ["PAPP"] = 0.30m },
            new Dictionary<string, string> { ["CATEGORIE"] = "13" }, ProfilFiscal.Standard);

    [Fact]
    public async Task Bulletin_enseignant_de_bout_en_bout_depuis_la_base()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);

        var repo = new PayrollReadRepository(scope.Conn);
        // PDLP-G105 = "Professeur de l'Ecole primaire" (titulaire), GE-ISSRP45-DIRECT (J4F).
        var input = await Charger(repo, "PDLP-G105");
        Assert.True(input.IsSuccess, input.IsFailure ? input.Error.Message : null);

        // Les formules ont bien été lues en base (pas de valeur codée en dur).
        var traitement = input.Value.Rubriques.Single(r => r.Id == "TRAITEMENT");
        Assert.Equal("(INDICE_MIN + INDICE_ECH) * VPI", traitement.Expression);
        Assert.Contains(input.Value.Rubriques, r => r.Id == "ISSRP_45");
        Assert.DoesNotContain(input.Value.Rubriques, r => r.Id == "ISSRP_30"); // pas de formule seedée

        // Barème IRG 2022 chargé pour une paie de 2025.
        Assert.NotNull(input.Value.RegleIrg);
        Assert.Equal("IRG-PER-2022", input.Value.RegleIrg!.Code);

        var pipeline = new CalculationPipeline(new ArrondiService(ModeArrondi.DinarPlusProche));
        var bulletin = pipeline.Calculer(input.Value);
        Assert.True(bulletin.IsSuccess, bulletin.IsFailure ? bulletin.Error.Message : null);
        var b = bulletin.Value;

        Assert.Equal(30510m, Ligne(b, "TRAITEMENT"));
        Assert.Equal(5202m, Ligne(b, "EXP_PEDAG"));
        Assert.Equal(9153m, Ligne(b, "PAPP"));
        Assert.Equal(13730m, Ligne(b, "ISSRP_45"));
        // QUALIF : CAT=13 ≥ 13 → tranche 45 % → TRT × 0.45 = 30510 × 0.45 = 13 729,5 → 13 730 DA.
        Assert.Equal(13730m, Ligne(b, "QUALIF"));
        // DOC_PEDAG : CAT=13 ≥ 13 → forfait 3 000 DA (barème lu en base, pas codé en dur).
        Assert.Equal(3000m, Ligne(b, "DOC_PEDAG"));
        Assert.Equal(75325m, b.TotalGains);
        Assert.Equal(6779m, Ligne(b, "SS"));
        Assert.Equal(68546m, b.AssietteImposable);
        Assert.Equal(10807m, b.Irg);
        Assert.Equal(57739m, b.Net);

        // J4.d — Audit Engine : une étape par rubrique calculée, dans l'ordre du
        // pipeline, lu depuis la base (rien codé en dur).
        // Ordre = OrdreCalcul lu en base (TRAITEMENT 100, QUALIF 206, DOC_PEDAG 207,
        // EXP_PEDAG 210, PAPP 220, ISSRP_45 230), puis cotisations, puis IRG.
        var etapesGain = b.Audit.Etapes.Where(e => e.Eligible).Select(e => e.RubriqueId).ToList();
        Assert.Equal(
            new[] { "TRAITEMENT", "QUALIF", "DOC_PEDAG", "EXP_PEDAG", "PAPP", "ISSRP_45", "SS", "IRG" },
            etapesGain);

        // J4.d — Explainability Engine : la ligne QUALIF porte la formule et les
        // variables réellement lues (TRT, la valeur du barème résolue par bareme()).
        var ligneQualif = b.Lignes.Single(l => l.RubriqueId == "QUALIF");
        Assert.Equal("TRT * bareme(QUALIF, CATEGORIE)", ligneQualif.Explication.Formule);
        Assert.Contains(ligneQualif.Explication.Variables, v => v.Nom == "TRT" && v.Valeur == 30510m);

        // La ligne IRG porte le détail multi-étapes déjà produit par IrgCalculator
        // (brut → abattement → lissage), plus dupliqué dans un string.
        var ligneIrg = b.Lignes.Single(l => l.RubriqueId == "IRG");
        Assert.NotNull(ligneIrg.Explication.DetailIrg);
        Assert.Equal(10807m, Math.Round(ligneIrg.Explication.DetailIrg!.Final, 0));
    }

    [Fact]
    public async Task Enseignant_hors_groupe_ISSRP_n_a_pas_la_prime()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);

        var repo = new PayrollReadRepository(scope.Conn);
        // A-G048 = "Administrateur" — NON ÉLIGIBLE à l'ISSRP (J4F : corps hors EN).
        var input = await Charger(repo, "A-G048");
        Assert.True(input.IsSuccess);

        var b = new CalculationPipeline(new ArrondiService()).Calculer(input.Value).Value;
        Assert.DoesNotContain(b.Lignes, l => l.RubriqueId == "ISSRP_45");
        Assert.Equal(61595m, b.TotalGains);   // 30510 + 5202 + 9153 + 13730 (QUALIF) + 3000 (DOC_PEDAG)
    }

    [Fact]
    public async Task Enseignant_grade_conditionnel_origine_ENSEIGNANT_a_45_pourcent()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);

        // SDL-G007 = "Educateur specialise en soutien Educatif" — grade
        // conditionnel (Q-C1) : 45 % si ORIGINE_STATUTAIRE = ENSEIGNANT.
        var repo = new PayrollReadRepository(scope.Conn);
        var input = await Charger(repo, "SDL-G007");
        Assert.True(input.IsSuccess);

        var b = new CalculationPipeline(new ArrondiService()).Calculer(input.Value).Value;
        Assert.Contains(b.Lignes, l => l.RubriqueId == "ISSRP_45");
        Assert.DoesNotContain(b.Lignes, l => l.RubriqueId == "ISSRP_30");
    }

    [Fact]
    public async Task Bulletin_enseignant_depuis_un_agent_reel_seede_en_base()
    {
        // Phase 5, jalon D + VariableEngine — preuve que ni l'AgentContext
        // (Enseignant() ci-dessus) ni les variables de base (VariablesBase
        // ci-dessus) ne sont plus construits à la main : l'AgentContext est
        // résolu depuis Agents/Carrieres/AgentAttributs par
        // AgentCarriereRepository, et INDICE_MIN/INDICE_ECH/VPI/TBASE/TRT/ECH/
        // CAT sont résolus depuis GrilleIndiciaire/IndicesEchelon/ValeurPoint
        // par VariableRepository — la dette signalée depuis J4.c est soldée.
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);
        SeedAgentReel(scope.Conn);

        var contexteRepo = new AgentCarriereRepository(scope.Conn);
        var contexte = await contexteRepo.ResoudreAsync("A-PILOTE", "2025-06-01");
        Assert.True(contexte.IsSuccess, contexte.IsFailure ? contexte.Error.Message : null);
        Assert.Equal("PDLP-G105", contexte.Value.Grade);
        Assert.Equal("ENSEIGNANT", contexte.Value.OrigineStatutaire);

        var variableRepo = new VariableRepository(scope.Conn);
        var variables = await variableRepo.ResoudreAsync(contexte.Value, "2025-06-01");
        Assert.True(variables.IsSuccess, variables.IsFailure ? variables.Error.Message : null);

        var repo = new PayrollReadRepository(scope.Conn);
        var input = await repo.ChargerAsync(
            contexte.Value, "2025-06-01", variables.Value,
            new Dictionary<string, decimal> { ["PAPP"] = 0.30m },
            new Dictionary<string, string> { ["CATEGORIE"] = "13" }, ProfilFiscal.Standard);
        Assert.True(input.IsSuccess, input.IsFailure ? input.Error.Message : null);

        var bulletin = new CalculationPipeline(new ArrondiService(ModeArrondi.DinarPlusProche)).Calculer(input.Value);
        Assert.True(bulletin.IsSuccess, bulletin.IsFailure ? bulletin.Error.Message : null);

        // Identique au bulletin du test « depuis la base » (même grade PDLP-G105,
        // même CATEGORIE/ECHELON) — seule la provenance de l'agent change.
        Assert.Equal(57739m, bulletin.Value.Net);
    }

    private static void SeedAgentReel(SqliteConnection conn)
    {
        Exec(conn, """
            INSERT INTO Filieres (Id, Libelle, CreatedAt, Hash) VALUES ('ENSEIGNANT', 'Enseignant', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ('PDLP', 'Prof. École primaire', 'ENSEIGNANT', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ('PDLP-G105', 'Professeur de l''Ecole primaire', 'PDLP', 1, '2026-01-01T00:00:00Z', 'h');
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

    private static decimal Ligne(Bulletin b, string id) => b.Lignes.Single(l => l.RubriqueId == id).Montant;

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
