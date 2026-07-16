namespace PaieEducation.Domain.Agents;

/// <summary>
/// Données nécessaires à la création d'un agent et de sa carrière initiale
/// (<c>Agents</c> + <c>Carrieres</c>, V011). Un agent sans carrière n'est pas
/// résoluble par <c>AgentCarriereRepository</c> — les deux sont donc créés
/// ensemble, atomiquement.
/// </summary>
/// <param name="Matricule">Identifiant métier unique de l'agent (contrainte <c>UNIQUE</c>).</param>
/// <param name="Sexe"><c>"M"</c> ou <c>"F"</c>.</param>
/// <param name="SituationFamiliale"><c>"CELIBATAIRE"</c>, <c>"MARIE"</c>, <c>"DIVORCE"</c> ou <c>"VEUF"</c>.</param>
/// <param name="GradeId">Grade initial (référentiel).</param>
/// <param name="CategorieId">Catégorie initiale (référentiel).</param>
/// <param name="EchelonId">Échelon initial (référentiel).</param>
/// <param name="TypeContrat"><c>"STATUTAIRE"</c> ou <c>"CONTRACTUEL"</c>.</param>
/// <param name="FonctionId">Fonction particulière, optionnelle.</param>
/// <param name="EtablissementId">Établissement d'affectation, optionnel.</param>
/// <param name="NumeroDecision">Référence de la décision administrative de recrutement, optionnelle.</param>
public sealed record NouvelAgent(
    string Matricule,
    string Nom,
    string Prenom,
    string DateNaissance,
    string DateRecrutement,
    string Sexe,
    string SituationFamiliale,
    string GradeId,
    string CategorieId,
    string EchelonId,
    string TypeContrat,
    string? FonctionId = null,
    string? EtablissementId = null,
    string? NumeroDecision = null);
