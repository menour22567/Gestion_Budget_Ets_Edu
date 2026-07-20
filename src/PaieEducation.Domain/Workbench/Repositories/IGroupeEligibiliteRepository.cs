using PaieEducation.Domain.Common;
using PaieEducation.Shared.Results;

namespace PaieEducation.Domain.Workbench.Repositories;

/// <summary>
/// Écrit des en-têtes de groupe DNF (<c>GroupesEligibilite</c>, V009 § 1.4) —
/// symétrique en écriture de <see cref="IWorkbenchReadRepository.ListerGroupesParRubriqueAsync"/>.
/// Port du Domain implémenté par
/// <c>Infrastructure.Repositories.Workbench.GroupeEligibiliteRepository</c>.
/// Chantier P6 (audit du 19/07/2026) — premier chemin d'écriture pour cette
/// table (jusqu'ici seedée uniquement, cf. <c>groupes_dnf_issrp_v1.json</c>).
/// </summary>
/// <remarks>
/// Contrairement à <c>RubriqueBaremes</c>, l'<c>Id</c> d'un groupe est un code
/// métier stable choisi par l'appelant (ex. <c>GE-ISSRP45-ORIGINE</c>) — le
/// seed réel donne à chaque période de « la même » règle logique un Id
/// distinct (ex. <c>GE-ISSRP15-DIRECT</c> vs <c>GE-ISSRP15-HIST</c>) plutôt
/// que de faire porter le versionnement par l'Id lui-même. La clôture
/// (<see cref="CloreGroupeAsync"/>) ne remplace donc jamais un groupe par un
/// autre : elle se contente de fermer sa période (<c>DateFin</c>), jamais de
/// <c>DELETE</c> (ADR-0008).
/// </remarks>
public interface IGroupeEligibiliteRepository
{
    /// <summary>
    /// Crée un nouveau groupe DNF. Échoue explicitement (jamais une exception
    /// SQLite qui fuit) si <paramref name="groupeId"/> existe déjà
    /// (<see cref="Error.Conflict"/>), si la rubrique référencée n'existe pas
    /// (<see cref="Error.NotFound"/>), ou si <paramref name="messageId"/> est
    /// fourni mais ne référence aucune ligne <c>MessagesRegles</c>
    /// (<see cref="Error.NotFound"/>).
    /// </summary>
    /// <returns>L'Id (code métier) de la ligne créée.</returns>
    Task<Result<string>> DefinirGroupeAsync(
        string groupeId,
        string rubriqueId,
        string severite,
        string? messageId,
        int priorite,
        string dateEffet,
        string? dateFin,
        string? source,
        string createdBy,
        DateTimeOffset creeLe,
        CancellationToken ct = default,
        IUnitOfWork? uow = null);

    /// <summary>
    /// Clôture un groupe en vigueur (<c>DateFin</c> = <paramref name="dateFin"/>) —
    /// suppression logique, jamais de remplacement automatique. Échoue si le
    /// groupe n'existe pas (<see cref="Error.NotFound"/>) ou est déjà clos
    /// (<see cref="Error.Conflict"/>).
    /// </summary>
    Task<Result<string>> CloreGroupeAsync(
        string groupeId,
        string dateFin,
        CancellationToken ct = default,
        IUnitOfWork? uow = null);
}
