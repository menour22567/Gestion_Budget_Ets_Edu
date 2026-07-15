using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Domain.Workbench.Services;

/// <summary>
/// Résout la valeur d'un critère d'éligibilité côté agent. Extensibilité D3 :
/// un nouveau type de critère = ajouter une branche dans <see cref="Resoudre"/>.
/// Pas de migration, pas de recompilation du moteur (Open/Closed).
/// </summary>
/// <remarks>
/// Pour les attributs agent (D3), l'attribut est lu depuis
/// <c>AgentContext</c>. Pour les attributs grade, l'attribut est résolu via la
/// hiérarchie carrière (Phase 5). Pour les données calculées (ancienneté,
/// assiettes), c'est le calculateur de source de valeur qui s'en charge (P3).
/// </remarks>
public sealed class CritereEligibiliteResolver
{
    /// <summary>
    /// Renvoie la valeur du critère pour l'agent, ou <c>null</c> si non résolu.
    /// </summary>
    public object? Resoudre(CritereEligibilite critere, AgentContext agent)
    {
        ArgumentNullException.ThrowIfNull(critere);
        ArgumentNullException.ThrowIfNull(agent);

        return critere.Id switch
        {
            "FILIERE"            => agent.Filiere,
            "CORPS"              => agent.Corps,
            "GRADE"              => agent.Grade,
            "CATEGORIE"          => agent.Categorie,
            "FONCTION"           => agent.Fonction,
            "TYPE_CONTRAT"       => agent.TypeContrat,
            "ECHELON"            => agent.Echelon,
            "ANCIENNETE"         => agent.AncienneteAnnees,
            "TYPE_ETABLISSEMENT" => agent.TypeEtablissement,
            "ORIGINE_STATUTAIRE" => agent.OrigineStatutaire,
            _ => null   // critère inconnu : le moteur le signalera dans le diagnostic
        };
    }
}
