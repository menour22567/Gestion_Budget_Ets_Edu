namespace PaieEducation.Domain.Agents;

/// <summary>
/// Données de modification de l'identité d'un agent existant (<c>Agents</c>,
/// V011). Le <c>Matricule</c> reste immuable dans ce périmètre (identifiant
/// métier de recherche/affichage, distinct de la FK <c>Agents.Id</c> — une
/// correction de matricule est une opération administrative rare, hors
/// périmètre V1) ; la carrière (grade/catégorie/échelon/...) se modifie par un
/// événement de carrière séparé (<see cref="EvenementCarriere"/>), jamais ici.
/// </summary>
/// <param name="AgentId">Identifiant (GUID) de l'agent à modifier.</param>
/// <param name="Sexe"><c>"M"</c> ou <c>"F"</c>.</param>
/// <param name="SituationFamiliale"><c>"CELIBATAIRE"</c>, <c>"MARIE"</c>, <c>"DIVORCE"</c> ou <c>"VEUF"</c>.</param>
/// <param name="Statut"><c>"ACTIF"</c>, <c>"SUSPENDU"</c> ou <c>"RADIE"</c>.</param>
public sealed record AgentModifie(
    string AgentId,
    string Nom,
    string Prenom,
    string DateNaissance,
    string Sexe,
    string SituationFamiliale,
    string Statut);
