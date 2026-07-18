using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Domain.Workbench.Repositories;

/// <summary>
/// Crée des affectations suggérées (<c>AgentRubriques</c>, Statut=SUGGEREE,
/// V011, J3H §7). Port du Domain implémenté par
/// <c>Infrastructure.Repositories.Workbench.AgentRubriqueRepository</c>.
/// </summary>
public interface IAgentRubriqueRepository
{
    /// <summary>
    /// Crée une ligne <c>SUGGEREE</c> pour (<paramref name="agentId"/>,
    /// <paramref name="rubriqueId"/>, <paramref name="occurrence"/>) à
    /// <paramref name="dateEffet"/> avec la provenance <paramref name="origine"/>
    /// (format <c>GROUPE:&lt;Id&gt;@&lt;DateEffet&gt;</c>, J3H §7). Idempotent :
    /// si une ligne non-<c>SUPPRIMEE</c> couvre déjà cette date pour ce
    /// couple, ne fait rien et renvoie <c>Result.Success&lt;string?&gt;(null)</c>
    /// (no-op, pas une erreur — ré-exécuter la suggestion ne duplique jamais).
    /// </summary>
    /// <returns>L'Id (GUID) de la ligne créée, ou <c>null</c> si déjà suggérée/affectée.</returns>
    Task<Result<string?>> SuggererAsync(
        string agentId, string rubriqueId, int occurrence, string origine, string dateEffet,
        DateTimeOffset creeLe, CancellationToken ct = default);

    /// <summary>
    /// Liste les affectations d'un agent couvrant <paramref name="datePaie"/>
    /// — <c>SUPPRIMEE</c> incluses (traçabilité complète, J3H §7 : « l'ancienne
    /// reste en trace »).
    /// </summary>
    Task<Result<IReadOnlyList<AffectationRubrique>>> ListerParAgentAsync(
        string agentId, string datePaie, CancellationToken ct = default);

    /// <summary>
    /// Change le statut de la ligne <paramref name="agentRubriqueId"/>. Aucune
    /// transition n'est bloquée par une règle (J3H §7 : « le logiciel avertit,
    /// l'utilisateur décide ») — seule la sortie de l'état terminal
    /// <c>SUPPRIMEE</c> est refusée explicitement (<see cref="Error.Conflict"/>) ;
    /// <see cref="Error.NotFound"/> si <paramref name="agentRubriqueId"/>
    /// n'existe pas.
    /// </summary>
    /// <returns>L'Id de la ligne modifiée, en cas de succès.</returns>
    Task<Result<string>> ChangerStatutAsync(
        string agentRubriqueId, string nouveauStatut, DateTimeOffset maintenant, CancellationToken ct = default);
}
