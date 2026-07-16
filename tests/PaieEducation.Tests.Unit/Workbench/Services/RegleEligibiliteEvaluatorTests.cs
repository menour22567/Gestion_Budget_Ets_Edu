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
        Assert.Single(r.ConditionsNonSatisfaites);
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
            criteres: new Dictionary<string, CritereEligibilite> { ["CORPS"] = C("CORPS") });
        Assert.True(r.EstEligible);
    }

    [Fact]
    public void DNF_conditions_groupees_jamais_ignorees()
    {
        // Régression : avant correctif, les conditions groupées étaient ignorées
        // quand les en-têtes de groupes n'étaient pas fournis à l'évaluateur —
        // l'agent était déclaré éligible à tort (surcomptage du simulateur D8).
        // La DNF est désormais déduite du GroupeId porté par les conditions.
        var eval = new RegleEligibiliteEvaluator(new CritereEligibiliteResolver());
        var r = eval.Evaluer("ISSRP_45", Agent("PELP"), "2025-06-15",
            conditions: new[] { Cond("C1", "ISSRP_45", "CORPS", Operateur.Egal, "PEM", groupeId: "G1") },
            criteres: new Dictionary<string, CritereEligibilite> { ["CORPS"] = C("CORPS") });
        Assert.False(r.EstEligible);
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
        // Cas 1 : agent en PEM, origine INCONNU → éligible (groupe A satisfait)
        var r1 = eval.Evaluer("ISSRP_45", Agent("PEM", origine: "INCONNU"), "2025-06-15",
            conditions, criteres);
        Assert.True(r1.EstEligible);

        // Cas 2 : agent en CENSEUR, origine ENSEIGNANT → éligible (groupe B satisfait)
        var r2 = eval.Evaluer("ISSRP_45", Agent("CENSEUR", origine: "ENSEIGNANT"), "2025-06-15",
            conditions, criteres);
        Assert.True(r2.EstEligible);

        // Cas 3 : agent en CENSEUR, origine AUTRE → non éligible (aucun groupe satisfait)
        var r3 = eval.Evaluer("ISSRP_45", Agent("CENSEUR", origine: "AUTRE"), "2025-06-15",
            conditions, criteres);
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
            });
        Assert.False(r.EstEligible);
        Assert.Equal(2, r.ConditionsNonSatisfaites.Count);   // les 2 conditions du groupe non satisfaites
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
            criteres: new Dictionary<string, CritereEligibilite> { ["CORPS"] = C("CORPS") });
        Assert.True(r.EstEligible);
    }

    // --- Contrat d'explicabilité (J4.e § 7.1, lot 2-restes) ---

    [Fact]
    public void Explication_porte_aussi_les_conditions_satisfaites_avec_valeur_agent()
    {
        // « Pourquoi cette rubrique ? » a besoin des conditions SATISFAITES :
        // critère, opérateur, valeur attendue, valeur de l'agent.
        var eval = new RegleEligibiliteEvaluator(new CritereEligibiliteResolver());
        var r = eval.Evaluer("ISSRP_45", Agent("PEM"), "2025-06-15",
            conditions: new[] { Cond("C1", "ISSRP_45", "CORPS", Operateur.Egal, "PEM", groupeId: "GA") },
            criteres: new Dictionary<string, CritereEligibilite> { ["CORPS"] = C("CORPS") });

        Assert.True(r.EstEligible);
        var groupe = Assert.Single(r.Groupes);
        Assert.Equal("GA", groupe.GroupeId);
        Assert.True(groupe.Satisfait);
        var cond = Assert.Single(groupe.Conditions);
        Assert.True(cond.Satisfaite);
        Assert.Equal("CORPS", cond.CritereId);
        Assert.Equal("PEM", cond.ValeurAttendue);
        Assert.Equal("PEM", cond.ValeurAgent);
        Assert.Null(cond.Detail);
    }

    [Fact]
    public void Explication_DNF_evalue_tous_les_groupes_sans_court_circuit()
    {
        // L'explication est complète : le groupe satisfait ET le groupe non
        // satisfait sont tous deux présents dans le résultat.
        var eval = new RegleEligibiliteEvaluator(new CritereEligibiliteResolver());
        var r = eval.Evaluer("R", Agent("B"), "2025-06-15",
            conditions: new[]
            {
                Cond("C1", "R", "CORPS", Operateur.Egal, "B", groupeId: "G1"), // satisfait
                Cond("C2", "R", "CORPS", Operateur.Egal, "A", groupeId: "G2"), // non satisfait
            },
            criteres: new Dictionary<string, CritereEligibilite> { ["CORPS"] = C("CORPS") });

        Assert.True(r.EstEligible);
        Assert.Equal(2, r.Groupes.Count);
        Assert.True(r.Groupes.Single(g => g.GroupeId == "G1").Satisfait);
        Assert.False(r.Groupes.Single(g => g.GroupeId == "G2").Satisfait);
    }

    [Fact]
    public void Explication_conditions_communes_dans_groupe_null()
    {
        var eval = new RegleEligibiliteEvaluator(new CritereEligibiliteResolver());
        var r = eval.Evaluer("R", Agent("PEM"), "2025-06-15",
            conditions: new[]
            {
                Cond("C0", "R", "CORPS", Operateur.Egal, "PEM"),                  // commune
                Cond("C1", "R", "ORIGINE_STATUTAIRE", Operateur.Egal, "ENSEIGNANT", groupeId: "G1"),
            },
            criteres: new Dictionary<string, CritereEligibilite>
            {
                ["CORPS"] = C("CORPS"),
                ["ORIGINE_STATUTAIRE"] = C("ORIGINE_STATUTAIRE", SourceResolution.AttributAgent),
            });

        Assert.True(r.EstEligible);
        var communes = r.Groupes.Single(g => g.GroupeId is null);
        Assert.True(communes.Satisfait);
        Assert.Equal("C0", Assert.Single(communes.Conditions).ConditionId);
    }

    [Fact]
    public void Abstention_critere_non_resolu_condition_non_satisfaite_avec_detail()
    {
        // ADR-0009 : information absente ⇒ jamais de droit déduit. Le critère
        // GRADE n'est pas renseigné dans le dossier → condition non satisfaite,
        // ValeurAgent null, Detail explicable — jamais d'exception.
        var eval = new RegleEligibiliteEvaluator(new CritereEligibiliteResolver());
        var r = eval.Evaluer("R", Agent(grade: null), "2025-06-15",
            conditions: new[] { Cond("C1", "R", "GRADE", Operateur.In, "PEM-G105,PEM-G106") },
            criteres: new Dictionary<string, CritereEligibilite> { ["GRADE"] = C("GRADE") });

        Assert.False(r.EstEligible);
        var cond = Assert.Single(r.ConditionsNonSatisfaites);
        Assert.Null(cond.ValeurAgent);
        Assert.Contains("non résolu", cond.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Explication_critere_inconnu_detail_explicable_sans_exception()
    {
        var eval = new RegleEligibiliteEvaluator(new CritereEligibiliteResolver());
        var r = eval.Evaluer("R", Agent(), "2025-06-15",
            conditions: new[] { Cond("C1", "R", "ZONE", Operateur.Egal, "SUD") },
            criteres: new Dictionary<string, CritereEligibilite>()); // ZONE absent du dictionnaire

        Assert.False(r.EstEligible);
        var cond = Assert.Single(r.ConditionsNonSatisfaites);
        Assert.Contains("inconnu", cond.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Determinisme_memes_entrees_memes_sorties()
    {
        var eval = new RegleEligibiliteEvaluator(new CritereEligibiliteResolver());
        var conditions = new[]
        {
            Cond("C1", "R", "CORPS", Operateur.Egal, "CENSEUR", groupeId: "G"),
            Cond("C2", "R", "ORIGINE_STATUTAIRE", Operateur.Egal, "ENSEIGNANT", groupeId: "G"),
        };
        var criteres = new Dictionary<string, CritereEligibilite>
        {
            ["CORPS"] = C("CORPS"),
            ["ORIGINE_STATUTAIRE"] = C("ORIGINE_STATUTAIRE", SourceResolution.AttributAgent),
        };
        var r1 = eval.Evaluer("R", Agent("CENSEUR"), "2025-06-15", conditions, criteres);
        var r2 = eval.Evaluer("R", Agent("CENSEUR"), "2025-06-15", conditions, criteres);
        Assert.Equal(r1.EstEligible, r2.EstEligible);
        Assert.Equal(
            r1.Groupes.Select(g => (g.GroupeId, g.Satisfait)),
            r2.Groupes.Select(g => (g.GroupeId, g.Satisfait)));
    }
}
