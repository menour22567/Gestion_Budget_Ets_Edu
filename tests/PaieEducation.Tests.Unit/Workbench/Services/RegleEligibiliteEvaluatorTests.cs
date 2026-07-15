using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Tests.Unit.Workbench.Services;

/// <summary>
/// Tests de <see cref="RegleEligibiliteEvaluator"/>. Couvre la sémantique
/// V008 (ET plat) et la DNF V009 (ET dans groupe, OU entre groupes).
/// Cas métier de référence : ISSRP_45 (D5 — DNF).
/// </summary>
public class RegleEligibiliteEvaluatorTests
{
    private static CritereEligibilite C(string id, SourceResolution res = SourceResolution.Carriere)
        => CritereEligibilite.Creer(id, id, TypeValeurCritere.Enum, res);

    private static ConditionEligibilite Cond(string id, string rub, string critereId,
                                              Operateur op, string valeur, string? groupeId = null,
                                              string eff = "2025-01-01", string? fin = null)
        => ConditionEligibilite.Creer(id, rub, critereId, op, valeur, groupeId,
            PeriodeReglementaire.Creer(eff, fin));

    private static GroupeEligibilite Groupe(string id, string rub, Severite sev = Severite.ObligatoireReglementaire)
        => GroupeEligibilite.Creer(id, rub, sev, messageId: null, priorite: 100,
            PeriodeReglementaire.Creer("2025-01-01", null), source: null);

    private static AgentContext Agent(string corps = "PEM", string? grade = null,
                                       string? origine = "ENSEIGNANT")
        => new(Filiere: "ENSEIGNANT", Corps: corps, Grade: grade, Categorie: 7,
               Echelon: 5, AncienneteAnnees: 10, Fonction: null, TypeContrat: "STATUTAIRE",
               TypeEtablissement: null, OrigineStatutaire: origine,
               Note: 0.35m, ValeurPointIndiciaire: 45m,
               AssietteCotisable: null, AssietteImposable: null);

    // --- ET plat (V008) ---

    [Fact]
    public void V008_et_plat_une_seule_condition_satisfaite_renvoie_eligible()
    {
        var eval = new RegleEligibiliteEvaluator(new CritereEligibiliteResolver());
        var r = eval.Evaluer("ISSRP_45", Agent("PEM"), "2025-06-15",
            conditions: new[] { Cond("C1", "ISSRP_45", "CORPS", Operateur.Egal, "PEM") },
            criteres: new Dictionary<string, CritereEligibilite> { ["CORPS"] = C("CORPS") });
        Assert.True(r.EstEligible);
    }

    [Fact]
    public void V008_et_plat_une_condition_non_satisfaite_renvoie_ineligible()
    {
        var eval = new RegleEligibiliteEvaluator(new CritereEligibiliteResolver());
        var r = eval.Evaluer("ISSRP_45", Agent("PEM"), "2025-06-15",
            conditions: new[] { Cond("C1", "ISSRP_45", "CORPS", Operateur.Egal, "PELP") },
            criteres: new Dictionary<string, CritereEligibilite> { ["CORPS"] = C("CORPS") });
        Assert.False(r.EstEligible);
        Assert.Single(r.Diagnostics);
    }

    [Fact]
    public void V008_aucune_condition_renvoie_eligible()
    {
        // Une rubrique sans condition est éligible partout (RM-040 : vide ⊨ vraie).
        var eval = new RegleEligibiliteEvaluator(new CritereEligibiliteResolver());
        var r = eval.Evaluer("LIBRE", Agent(), "2025-06-15",
            conditions: Array.Empty<ConditionEligibilite>(),
            criteres: new Dictionary<string, CritereEligibilite>());
        Assert.True(r.EstEligible);
    }

    // --- DNF (V009, D5) ---

    [Fact]
    public void DNF_groupe_unique_satisfait_renvoie_eligible()
    {
        var eval = new RegleEligibiliteEvaluator(new CritereEligibiliteResolver());
        var r = eval.Evaluer("ISSRP_45", Agent("PEM"), "2025-06-15",
            conditions: new[] { Cond("C1", "ISSRP_45", "CORPS", Operateur.Egal, "PEM", groupeId: "G1") },
            criteres: new Dictionary<string, CritereEligibilite> { ["CORPS"] = C("CORPS") },
            groupes: new[] { Groupe("G1", "ISSRP_45") });
        Assert.True(r.EstEligible);
    }

    [Fact]
    public void DNF_ISSRP_45_cas_pedagogique_direct_eligible()
    {
        // Issu de J3G : ISSRP_45 = "Grade direct" OU ("Grade conditionnel" ET "Origine=ENSEIGNANT")
        var eval = new RegleEligibiliteEvaluator(new CritereEligibiliteResolver());
        var conditions = new[]
        {
            // Groupe A : grade direct (PEM)
            Cond("C-A1", "ISSRP_45", "CORPS", Operateur.Egal, "PEM", groupeId: "GA"),
            // Groupe B : grade conditionnel (CENSEUR) ET origine ENSEIGNANT
            Cond("C-B1", "ISSRP_45", "CORPS",  Operateur.Egal, "CENSEUR",       groupeId: "GB"),
            Cond("C-B2", "ISSRP_45", "ORIGINE_STATUTAIRE", Operateur.Egal, "ENSEIGNANT", groupeId: "GB"),
        };
        var criteres = new Dictionary<string, CritereEligibilite>
        {
            ["CORPS"] = C("CORPS"),
            ["ORIGINE_STATUTAIRE"] = C("ORIGINE_STATUTAIRE", SourceResolution.AttributAgent),
        };
        var groupes = new[] { Groupe("GA", "ISSRP_45"), Groupe("GB", "ISSRP_45") };

        // Cas 1 : agent en PEM, origine INCONNU → éligible (groupe A satisfait)
        var r1 = eval.Evaluer("ISSRP_45", Agent("PEM", origine: "INCONNU"), "2025-06-15",
            conditions, criteres, groupes);
        Assert.True(r1.EstEligible);

        // Cas 2 : agent en CENSEUR, origine ENSEIGNANT → éligible (groupe B satisfait)
        var r2 = eval.Evaluer("ISSRP_45", Agent("CENSEUR", origine: "ENSEIGNANT"), "2025-06-15",
            conditions, criteres, groupes);
        Assert.True(r2.EstEligible);

        // Cas 3 : agent en CENSEUR, origine AUTRE → non éligible (aucun groupe satisfait)
        var r3 = eval.Evaluer("ISSRP_45", Agent("CENSEUR", origine: "AUTRE"), "2025-06-15",
            conditions, criteres, groupes);
        Assert.False(r3.EstEligible);
    }

    [Fact]
    public void DNF_groupe_avec_une_condition_non_satisfaite_compromet_le_groupe()
    {
        var eval = new RegleEligibiliteEvaluator(new CritereEligibiliteResolver());
        // L'agent a un corps "INCONNU" (≠ CENSEUR) et une origine "AUTRE" (≠ ENSEIGNANT) :
        // les DEUX conditions du groupe échouent, donc le groupe n'est pas satisfait,
        // donc la rubrique n'est pas éligible.
        var r = eval.Evaluer("ISSRP_45", Agent("INCONNU", origine: "AUTRE"), "2025-06-15",
            conditions: new[]
            {
                Cond("C1", "ISSRP_45", "CORPS",  Operateur.Egal, "CENSEUR",       groupeId: "G"),
                Cond("C2", "ISSRP_45", "ORIGINE_STATUTAIRE", Operateur.Egal, "ENSEIGNANT", groupeId: "G"),
            },
            criteres: new Dictionary<string, CritereEligibilite>
            {
                ["CORPS"] = C("CORPS"),
                ["ORIGINE_STATUTAIRE"] = C("ORIGINE_STATUTAIRE", SourceResolution.AttributAgent),
            },
            groupes: new[] { Groupe("G", "ISSRP_45") });
        Assert.False(r.EstEligible);
        Assert.Equal(2, r.Diagnostics.Count);   // les 2 conditions du groupe non satisfaites
    }

    [Fact]
    public void DNF_plusieurs_groupes_un_seul_suffit_pour_eligible()
    {
        // OU entre groupes : si un seul groupe est satisfait, c'est éligible.
        var eval = new RegleEligibiliteEvaluator(new CritereEligibiliteResolver());
        var r = eval.Evaluer("R", Agent("B"), "2025-06-15",
            conditions: new[]
            {
                // Groupe G1 : corps A (NON)
                Cond("C1", "R", "CORPS", Operateur.Egal, "A", groupeId: "G1"),
                // Groupe G2 : corps B (OUI) — un seul membre suffit
                Cond("C2", "R", "CORPS", Operateur.Egal, "B", groupeId: "G2"),
            },
            criteres: new Dictionary<string, CritereEligibilite> { ["CORPS"] = C("CORPS") },
            groupes: new[] { Groupe("G1", "R"), Groupe("G2", "R") });
        Assert.True(r.EstEligible);
    }
}
