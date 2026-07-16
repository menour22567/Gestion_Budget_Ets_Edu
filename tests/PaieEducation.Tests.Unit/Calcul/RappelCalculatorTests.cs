using PaieEducation.Domain.Calcul.Audit;
using PaieEducation.Domain.Calcul.Explicabilite;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Rappels;
using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Tests.Unit.Calcul;

/// <summary>
/// Tests du <see cref="RappelCalculator"/> (J3C §11, RM-102, ADR-0008). Primitive
/// pure : compare un snapshot d'époque à un recalcul, sans jamais réévaluer le
/// passé lui-même (l'appelant fournit les deux résultats déjà calculés).
/// </summary>
public class RappelCalculatorTests
{
    private static readonly ExplicationLigne ExplicationVide = new("", Array.Empty<VariableUtilisee>());
    private static readonly JournalAudit AuditVide = new(Array.Empty<EtapeAudit>());

    private static BulletinLigne Ligne(string id, decimal montant)
        => new(id, NatureRubrique.Gain, montant, true, true, ExplicationVide);

    private static Bulletin Bulletin(params BulletinLigne[] lignes)
        => new(lignes, lignes.Sum(l => l.Montant), 0m, 0m, 0m, 0m, lignes.Sum(l => l.Montant), AuditVide);

    private static BulletinSnapshot Snapshot(Bulletin b) => new(
        Input: DummyInput(), Resultat: b, CapturesLe: "2025-01-01T00:00:00Z");

    private static PayrollInput DummyInput() => new(
        Agent: new AgentContext("ENSEIGNANT", "PEM", null, 13, 5, 10, null, "STATUTAIRE",
            null, "ENSEIGNANT", 0.3m, 45m, null, null),
        DatePaie: "2025-01-01",
        Variables: new Dictionary<string, decimal>(),
        SourcesValeur: new Dictionary<string, decimal>(),
        ClesBareme: new Dictionary<string, string>(),
        Rubriques: Array.Empty<RubriqueCalcul>(),
        Baremes: Array.Empty<PaieEducation.Domain.Workbench.ValueObjects.BaremeValue>(),
        Conditions: Array.Empty<ConditionEligibilite>(),
        Criteres: new Dictionary<string, CritereEligibilite>(),
        Cotisations: Array.Empty<CotisationCalcul>(),
        Profil: PaieEducation.Domain.Calcul.Irg.ProfilFiscal.Standard,
        RegleIrg: null);

    [Fact]
    public void Montants_identiques_ne_produisent_aucun_rappel()
    {
        var ancien = Snapshot(Bulletin(Ligne("ISSRP_45", 13730m)));
        var nouveau = Bulletin(Ligne("ISSRP_45", 13730m));

        var rappels = new RappelCalculator().Calculer(ancien, nouveau);

        Assert.Empty(rappels);
    }

    [Fact]
    public void Montant_modifie_produit_une_ligne_de_rappel_avec_le_bon_delta()
    {
        // Ex. taux ISSRP passé de 45% (13730) à un nouveau montant après évolution
        // réglementaire à effet rétroactif — le rappel est la différence.
        var ancien = Snapshot(Bulletin(Ligne("ISSRP_45", 13730m)));
        var nouveau = Bulletin(Ligne("ISSRP_45", 15000m));

        var rappels = new RappelCalculator().Calculer(ancien, nouveau);

        var r = Assert.Single(rappels);
        Assert.Equal("ISSRP_45", r.RubriqueId);
        Assert.Equal(13730m, r.MontantAncien);
        Assert.Equal(15000m, r.MontantNouveau);
        Assert.Equal(1270m, r.Delta);
    }

    [Fact]
    public void Rubrique_nouvellement_eligible_produit_un_rappel_positif_depuis_zero()
    {
        var ancien = Snapshot(Bulletin(Ligne("TRAITEMENT", 30000m)));
        var nouveau = Bulletin(Ligne("TRAITEMENT", 30000m), Ligne("QUALIF", 13500m));

        var rappels = new RappelCalculator().Calculer(ancien, nouveau);

        var r = Assert.Single(rappels);
        Assert.Equal("QUALIF", r.RubriqueId);
        Assert.Equal(0m, r.MontantAncien);
        Assert.Equal(13500m, r.MontantNouveau);
        Assert.Equal(13500m, r.Delta);
    }

    [Fact]
    public void Rubrique_devenue_ineligible_produit_un_rappel_negatif_vers_zero()
    {
        var ancien = Snapshot(Bulletin(Ligne("TRAITEMENT", 30000m), Ligne("QUALIF", 13500m)));
        var nouveau = Bulletin(Ligne("TRAITEMENT", 30000m));

        var rappels = new RappelCalculator().Calculer(ancien, nouveau);

        var r = Assert.Single(rappels);
        Assert.Equal("QUALIF", r.RubriqueId);
        Assert.Equal(13500m, r.MontantAncien);
        Assert.Equal(0m, r.MontantNouveau);
        Assert.Equal(-13500m, r.Delta);
    }

    [Fact]
    public void Plusieurs_lignes_modifiees_produisent_plusieurs_rappels_tries_par_rubrique()
    {
        var ancien = Snapshot(Bulletin(Ligne("PAPP", 9000m), Ligne("ISSRP_45", 13000m)));
        var nouveau = Bulletin(Ligne("PAPP", 9500m), Ligne("ISSRP_45", 13500m));

        var rappels = new RappelCalculator().Calculer(ancien, nouveau);

        Assert.Equal(2, rappels.Count);
        Assert.Equal("ISSRP_45", rappels[0].RubriqueId); // ordre alphabétique
        Assert.Equal("PAPP", rappels[1].RubriqueId);
    }
}
