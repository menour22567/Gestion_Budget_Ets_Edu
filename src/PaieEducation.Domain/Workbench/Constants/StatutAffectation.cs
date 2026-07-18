namespace PaieEducation.Domain.Workbench.Constants;

/// <summary>
/// États du cycle de vie d'une affectation agent-rubrique (J3H §7, diagramme
/// d'état). Constantes sémantiques du Domain — correspondant exactement aux
/// valeurs stockées dans la colonne <c>AgentRubriques.Statut</c>.
/// </summary>
/// <remarks>
/// État terminal : <c>SUPPRIMEE</c> — toute transition depuis cet état est
/// refusée par <c>IAgentRubriqueRepository.ChangerStatutAsync</c>.
/// </remarks>
public static class StatutAffectation
{
    /// <summary>Affectation suggérée par le système (état initial).</summary>
    public const string Suggerer = "SUGGEREE";

    /// <summary>Affectation acceptée par l'agent ou l'administration.</summary>
    public const string Acceptee = "ACCEPTEE";

    /// <summary>Affectation suspendue (réactivable vers ACCEPTEE).</summary>
    public const string Suspendue = "SUSPENDUE";

    /// <summary>Affectation supprimée (état terminal — créer une nouvelle ligne).</summary>
    public const string Supprimee = "SUPPRIMEE";
}
