namespace PaieEducation.Domain.Agents;

/// <summary>
/// Nouvel événement de carrière pour un agent existant (<c>Carrieres</c>,
/// V011) : avancement d'échelon, promotion de grade/catégorie, mutation
/// d'établissement, changement de fonction ou de type de contrat. Insère une
/// nouvelle version de carrière et ferme la précédente (même stratégie de
/// continuité temporelle que <c>DefinirParametreRubrique</c>/<c>DefinirValeurBareme</c> :
/// jamais de recouvrement, jamais de réécriture rétroactive silencieuse) —
/// l'historique de carrière reste donc entièrement consultable.
/// </summary>
/// <param name="AgentId">Identifiant (GUID) de l'agent concerné.</param>
/// <param name="GradeId">Nouveau grade (référentiel).</param>
/// <param name="CategorieId">Nouvelle catégorie (référentiel).</param>
/// <param name="EchelonId">Nouvel échelon (référentiel).</param>
/// <param name="TypeContrat"><c>"STATUTAIRE"</c> ou <c>"CONTRACTUEL"</c>.</param>
/// <param name="DateEffet">Date d'effet du changement — doit être postérieure à la carrière en vigueur.</param>
/// <param name="Motif">Motif administratif (ex. « Avancement d'échelon », « Promotion de grade », « Mutation »).</param>
/// <param name="FonctionId">Fonction particulière, optionnelle.</param>
/// <param name="EtablissementId">Nouvel établissement d'affectation, optionnel.</param>
/// <param name="NumeroDecision">Référence de la décision administrative, optionnelle.</param>
public sealed record EvenementCarriere(
    string AgentId,
    string GradeId,
    string CategorieId,
    string EchelonId,
    string TypeContrat,
    string DateEffet,
    string Motif,
    string? FonctionId = null,
    string? EtablissementId = null,
    string? NumeroDecision = null,
    string? Source = null);
