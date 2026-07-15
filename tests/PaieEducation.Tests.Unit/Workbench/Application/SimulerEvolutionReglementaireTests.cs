using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Tests.Unit.Workbench.Application;

/// <summary>
/// Tests du use case <see cref="SimulerEvolutionReglementaire"/> (D8 — dry-run).
/// Couvre la validation de continuité temporelle (L-U8) et la production du
/// rapport d'impact structurel.
/// </summary>
public class SimulerEvolutionReglementaireTests
{
    private static SimulerEvolutionReglementaire.Demande D(string rub, string eff, string? fin,
        params (string Eff, string? Fin)[] existantes)
    {
        var periodesExistantes = existantes
            .Select(e => PeriodeReglementaire.Creer(e.Eff, e.Fin))
            .ToList();
        return new SimulerEvolutionReglementaire.Demande(
            RubriqueId: rub,
            Description: "Test",
            NouvellePeriode: PeriodeReglementaire.Creer(eff, fin),
            PeriodesExistantes: periodesExistantes);
    }

    [Fact]
    public void Simuler_refuse_evolution_qui_chevauche()
    {
        var useCase = new SimulerEvolutionReglementaire();
        var demande = D("DOC", "2011-06-01", "2013-12-31",
            ("2008-01-01", "2010-12-31"),
            ("2011-01-01", "2014-12-31"));   // chevauche avec 2011-06-01

        var r = useCase.Executer(demande);
        Assert.True(r.IsFailure);
        Assert.Contains("continuité", r.Error.Message);
    }

    [Fact]
    public void Simuler_refuse_evolution_qui_cree_un_trou()
    {
        var useCase = new SimulerEvolutionReglementaire();
        // La nouvelle période commence le 2012-06-01 mais la précédente finit le
        // 2012-01-31 — trou de 4 mois entre 2012-02-01 et 2012-05-31.
        var demande = D("DOC", "2012-06-01", "2013-12-31",
            ("2008-01-01", "2010-12-31"),
            ("2011-01-01", "2012-01-31"));

        var r = useCase.Executer(demande);
        Assert.True(r.IsFailure);
        Assert.Contains("continuité", r.Error.Message);
    }

    [Fact]
    public void Simuler_accepte_evolution_continue_et_produit_rapport()
    {
        var useCase = new SimulerEvolutionReglementaire();
        var demande = D("DOC", "2025-01-01", null,   // ouverte
            ("2008-01-01", "2010-12-31"),
            ("2011-01-01", "2024-12-31"));

        var r = useCase.Executer(demande);
        Assert.True(r.IsSuccess);
        Assert.Equal(0, r.Value.NbAgents);
        Assert.Equal(0m, r.Value.MontantTotalMensuel);
        Assert.Equal("2025-01-01", r.Value.PeriodeImpactee);
        Assert.Equal(0, r.Value.BulletinsAvertis);
    }

    [Fact]
    public void Simuler_refuse_RubriqueId_vide()
    {
        var useCase = new SimulerEvolutionReglementaire();
        var demande = new SimulerEvolutionReglementaire.Demande(
            RubriqueId: "",
            Description: "X",
            NouvellePeriode: PeriodeReglementaire.Creer("2025-01-01", null),
            PeriodesExistantes: Array.Empty<PeriodeReglementaire>());

        Assert.ThrowsAny<ArgumentException>(() => useCase.Executer(demande));
    }

    [Fact]
    public void Simuler_compte_les_agents_eligibles_quand_donnees_completes()
    {
        // 3 agents : 2 en PEM (éligibles), 1 en PELP (inéligible).
        // Condition : ISSRP_45 éligible si corps = PEM.
        var useCase = new SimulerEvolutionReglementaire();
        var conditions = new[]
        {
            ConditionEligibilite.Creer("C1", "ISSRP_45", "CORPS",
                Operateur.Egal, "PEM", null,
                PeriodeReglementaire.Creer("2025-01-01", null))
        };
        var criteres = new Dictionary<string, CritereEligibilite>
        {
            ["CORPS"] = CritereEligibilite.Creer("CORPS", "Corps", TypeValeurCritere.Enum, SourceResolution.Carriere)
        };
        var agents = new List<AgentContext>
        {
            new(Filiere: "ENSEIGNANT", Corps: "PEM", Grade: null, Categorie: 7, Echelon: 5,
                AncienneteAnnees: 10, Fonction: null, TypeContrat: "STATUTAIRE",
                TypeEtablissement: null, OrigineStatutaire: "ENSEIGNANT",
                Note: 0.35m, ValeurPointIndiciaire: 45m, AssietteCotisable: null, AssietteImposable: null),
            new(Filiere: "ENSEIGNANT", Corps: "PELP", Grade: null, Categorie: 7, Echelon: 5,
                AncienneteAnnees: 10, Fonction: null, TypeContrat: "STATUTAIRE",
                TypeEtablissement: null, OrigineStatutaire: "ENSEIGNANT",
                Note: 0.35m, ValeurPointIndiciaire: 45m, AssietteCotisable: null, AssietteImposable: null),
            new(Filiere: "ENSEIGNANT", Corps: "PEM", Grade: null, Categorie: 7, Echelon: 3,
                AncienneteAnnees: 5, Fonction: null, TypeContrat: "STATUTAIRE",
                TypeEtablissement: null, OrigineStatutaire: "AUTRE",
                Note: 0.40m, ValeurPointIndiciaire: 45m, AssietteCotisable: null, AssietteImposable: null),
        };
        var demande = new SimulerEvolutionReglementaire.Demande(
            RubriqueId: "ISSRP_45",
            Description: "Extension aux PEM",
            NouvellePeriode: PeriodeReglementaire.Creer("2025-01-01", null),
            PeriodesExistantes: Array.Empty<PeriodeReglementaire>(),
            AgentsCandidats: agents,
            ConditionsApres: conditions,
            Criteres: criteres);

        var r = useCase.Executer(demande);
        Assert.True(r.IsSuccess);
        Assert.Equal(2, r.Value.NbAgents);   // 2 sur 3 sont PEM
    }

    [Fact]
    public void Simuler_retourne_NbAgents_0_si_donnees_manquantes()
    {
        // Sans AgentsCandidats / ConditionsApres / Criteres, NbAgents = 0
        // (mode "validation de continuité uniquement").
        var useCase = new SimulerEvolutionReglementaire();
        var demande = new SimulerEvolutionReglementaire.Demande(
            RubriqueId: "ISSRP_45",
            Description: "Sans preview d'impact",
            NouvellePeriode: PeriodeReglementaire.Creer("2025-01-01", null),
            PeriodesExistantes: Array.Empty<PeriodeReglementaire>());

        var r = useCase.Executer(demande);
        Assert.True(r.IsSuccess);
        Assert.Equal(0, r.Value.NbAgents);
    }
}
