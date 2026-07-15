using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.Internal;

namespace PaieEducation.Domain.Workbench.ValueObjects;

/// <summary>
/// Condition d'éligibilité atomique (rubrique, critère, opérateur, valeur). Membre
/// d'un groupe (DNF, D5) si <see cref="GroupeId"/> est non null, sinon condition
/// commune (ET plat V008 inchangé).
/// </summary>
/// <remarks>
/// La sémantique de la valeur dépend du <see cref="CritereEligibilite"/> référencé
/// par <c>CritereId</c> : <c>TypeValeur</c> indique comment parser
/// <see cref="Valeur"/> côté moteur. Pour le <c>Operateur.In</c> /
/// <c>NotIn</c>, la valeur est une liste CSV (ex. <c>"PEM,PES,INSPECTION"</c>) —
/// cf. J3B RM-040 et la requête de résolution J3E § 9.5.
/// </remarks>
public sealed record ConditionEligibilite
{
    /// <summary>Identifiant unique de la condition.</summary>
    public string Id { get; }

    /// <summary>Rubrique cible (FK).</summary>
    public string RubriqueId { get; }

    /// <summary>Critère d'éligibilité (FK vers <c>CriteresEligibilite.Id</c>, R3).</summary>
    public string CritereId { get; }

    /// <summary>Opérateur de comparaison.</summary>
    public Operateur Operateur { get; }

    /// <summary>Valeur textuelle (sémantique selon <c>CritereEligibilite.TypeValeur</c>).</summary>
    public string Valeur { get; }

    /// <summary>
    /// <c>null</c> = condition commune (ET plat, comportement V008 inchangé).
    /// Non <c>null</c> = condition membre du groupe (ET dans le groupe, OU entre groupes).
    /// </summary>
    public string? GroupeId { get; }

    /// <summary>Période de validité.</summary>
    public PeriodeReglementaire Periode { get; }

    private ConditionEligibilite(
        string id,
        string rubriqueId,
        string critereId,
        Operateur operateur,
        string valeur,
        string? groupeId,
        PeriodeReglementaire periode)
    {
        Id = id;
        RubriqueId = rubriqueId;
        CritereId = critereId;
        Operateur = operateur;
        Valeur = valeur;
        GroupeId = groupeId;
        Periode = periode;
    }

    /// <summary>Fabrique validante.</summary>
    public static ConditionEligibilite Creer(
        string id,
        string rubriqueId,
        string critereId,
        Operateur operateur,
        string valeur,
        string? groupeId,
        PeriodeReglementaire periode)
    {
        Guard.AgainstNullOrWhiteSpace(id);
        Guard.AgainstNullOrWhiteSpace(rubriqueId);
        Guard.AgainstNullOrWhiteSpace(critereId);
        Guard.AgainstNullOrWhiteSpace(valeur);
        return new ConditionEligibilite(id, rubriqueId, critereId, operateur, valeur, groupeId, periode);
    }
}

