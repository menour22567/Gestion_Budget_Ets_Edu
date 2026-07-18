using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Constants;
using PaieEducation.Domain.Workbench.Calculators;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Infrastructure.Workbench.Calculators;

namespace PaieEducation.Tests.Integration.Workbench;

/// <summary>
/// Tests Lot 1.2 — vérifie que les 3 calculateurs signalés "non résolus en
/// V1" retournent désormais une valeur réelle (issue de la base) ou un
/// échec explicite — plus aucun message placeholder "non résolu en V1".
/// </summary>
public class SourceValeurCalculatorsTests
{
    private const string DatePaie = "2025-06-01";

    // ---------------------------------------------------------------------
    // INDICE_ECHELON — pur, lit depuis AgentContext
    // ---------------------------------------------------------------------

    [Fact]
    public void IndiceEchelon_retourne_la_valeur_portee_par_l_agent()
    {
        var calc = new IndiceEchelonCalculator();
        var agent = Agent(indiceEchelon: 578m);

        var result = calc.Calculer(agent, DatePaie);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal(578m, result.Value);
    }

    [Fact]
    public void IndiceEchelon_retourne_NotFound_si_indice_absent_du_snapshot()
    {
        var calc = new IndiceEchelonCalculator();
        var agent = Agent(indiceEchelon: null);

        var result = calc.Calculer(agent, DatePaie);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        // On s'assure que l'ancien message placeholder a bien disparu.
        Assert.DoesNotContain("non résolu", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IndiceEchelon_ne_confond_pas_avec_le_numero_dechelon()
    {
        // Risque identifié Lot 1.2 : un Echelon=5 (n°) ne doit pas être
        // confondu avec un Indice=5 (qui serait un montant aberrant).
        // Le calculateur lit IndiceEchelon, pas Echelon.
        var calc = new IndiceEchelonCalculator();
        var agent = Agent(echelon: 5, indiceEchelon: 578m);

        var result = calc.Calculer(agent, DatePaie);

        Assert.True(result.IsSuccess);
        Assert.Equal(578m, result.Value); // pas 5
    }

    // ---------------------------------------------------------------------
    // ANCIENNETE_PRIVEE — pur, lit depuis AgentContext (versionné)
    // ---------------------------------------------------------------------

    [Fact]
    public void AnciennetePrivee_retourne_la_valeur_depuis_l_attribut_agent()
    {
        var calc = new AnciennetePriveeCalculator();
        var agent = Agent(anciennetePriveeAnnees: 7);

        var result = calc.Calculer(agent, DatePaie);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public void AnciennetePrivee_retourne_NotFound_si_aucun_attribut_renseigne()
    {
        var calc = new AnciennetePriveeCalculator();
        var agent = Agent(anciennetePriveeAnnees: null);

        var result = calc.Calculer(agent, DatePaie);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        // Avant Lot 1.2 : 0 silencieux. Maintenant : abstention explicite.
        Assert.DoesNotContain("non résolu", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------
    // CONSTANTE_REGLEMENTAIRE — impur, via IRubriqueParametreLookup
    // ---------------------------------------------------------------------

    [Fact]
    public void ConstanteReglementaire_retourne_la_valeur_depuis_RubriqueParametres()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        // En V1, le calculator utilise son propre CodeSource comme clé de
        // lookup. Pour tester la résolution, on insère avec la clé
        // "CONSTANTE_REGLEMENTAIRE". Quand la propagation du contexte de
        // rubrique sera ajoutée, ce test évoluera pour utiliser la clé
        // métier (ex. "TAUX_45").
        InsererConstante(scope.Conn, "ISSRP", SourceValeurCodes.ConstanteReglementaire, "0.45", "2024-01-01");
        var calc = new ConstanteReglementaireCalculator(new RubriqueParametreLookup(scope.Conn));

        var result = calc.Calculer(Agent(), DatePaie);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal(0.45m, result.Value);
    }

    [Fact]
    public void ConstanteReglementaire_retourne_NotFound_si_aucun_parametre_actif()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        var calc = new ConstanteReglementaireCalculator(new RubriqueParametreLookup(scope.Conn));

        var result = calc.Calculer(Agent(), DatePaie);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        // Avant Lot 1.2 : "non résolue en V1 — la lecture de RubriqueParametres
        // est branchée en Phase 4". Maintenant : erreur ciblée sur la donnée.
        Assert.DoesNotContain("non résolu", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConstanteReglementaire_retourne_NotFound_pour_cle_inconnue()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        InsererConstante(scope.Conn, "ISSRP", "TAUX_45", "0.45", "2024-01-01");
        var calc = new ConstanteReglementaireCalculator(new RubriqueParametreLookup(scope.Conn));

        var result = calc.Calculer(Agent(), DatePaie);

        // Le calculator utilise CodeSource = "CONSTANTE_REGLEMENTAIRE" comme
        // clé de lookup en V1. Sans ligne avec cette clé, on est en NotFound.
        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static AgentContext Agent(
        int? echelon = 5, decimal? indiceEchelon = 100m, int? anciennetePriveeAnnees = null) =>
        new(
            Filiere: "ENSEIGNANT", Corps: "PEM", Grade: "PEM-G1",
            Categorie: 13, Echelon: echelon, AncienneteAnnees: 10,
            Fonction: null, TypeContrat: "STATUTAIRE", TypeEtablissement: "LYCEE",
            OrigineStatutaire: "ENSEIGNANT", Note: null, ValeurPointIndiciaire: 45m,
            AssietteCotisable: null, AssietteImposable: null,
            IndiceEchelon: indiceEchelon,
            AnciennetePriveeAnnees: anciennetePriveeAnnees);

    private static void InsererConstante(
        SqliteConnection c, string rubriqueId, string cle, string valeur, string dateEffet)
    {
        // Le scope est migré : on insère la rubrique puis son paramètre.
        SchemaTestSupport.Exec(c, """
            INSERT INTO Rubriques (Id, Libelle, Nature, BaseCalcul, Periodicite, OrdreCalcul,
                                   EstImposable, EstCotisable, EstAffectableManuellement, OccurrencesMultiples,
                                   Description, CreatedAt, Hash)
            VALUES ($rid, $rlib, 'GAIN', 'TBASE', 'MENSUELLE', 1, 1, 1, 0, 0, 'Test', '2026-01-01T00:00:00Z', 'h')
            ON CONFLICT(Id) DO NOTHING;
            """, ("$rid", rubriqueId), ("$rlib", $"Rubrique {rubriqueId}"));

        var id = $"RP-{rubriqueId}-{cle}-{dateEffet}";
        SchemaTestSupport.Exec(c, """
            INSERT INTO RubriqueParametres
                (Id, RubriqueId, Cle, Valeur, DateEffet, DateFin, Source, Hash, CreatedAt)
            VALUES ($id, $rid, $cle, $valeur, $de, NULL, 'test', 'h', '2026-01-01T00:00:00Z');
            """, ("$id", id), ("$rid", rubriqueId), ("$cle", cle), ("$valeur", valeur), ("$de", dateEffet));
    }
}
