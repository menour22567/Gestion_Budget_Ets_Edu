using PaieEducation.Domain.Common;
using PaieEducation.Shared.Results;

namespace PaieEducation.Domain.Workbench.Repositories;

/// <summary>
/// Écrit des conditions d'éligibilité (<c>ReglesEligibilite</c>, V009 étape 2) —
/// symétrique en écriture de <see cref="IWorkbenchReadRepository.ListerConditionsParRubriqueAsync"/>.
/// Port du Domain implémenté par
/// <c>Infrastructure.Repositories.Workbench.RegleEligibiliteRepository</c>.
/// Chantier P6 (audit du 19/07/2026) — premier chemin d'écriture pour cette
/// table (jusqu'ici seedée uniquement, cf. <c>groupes_dnf_issrp_v1.json</c>).
/// </summary>
/// <remarks>
/// Même patron « ferme puis insère » que <c>IRubriqueBaremeRepository</c>
/// (ADR-0008) : l'identité logique d'une condition est le triplet
/// <c>(RubriqueId, CritereId, GroupeId)</c> (<c>GroupeId</c> pouvant être
/// <c>null</c> = condition commune) — une nouvelle version de la même
/// condition (ex. la liste de grades ISSRP change de date d'effet) ferme la
/// précédente à la veille plutôt que de la réécrire. <see cref="CloreRegleAsync"/>
/// permet en plus de fermer une condition sans la remplacer (suppression
/// logique pure), jamais de <c>DELETE</c>.
/// </remarks>
public interface IRegleEligibiliteRepository
{
    /// <summary>
    /// Définit une nouvelle condition pour le triplet
    /// <c>(rubriqueId, critereId, groupeId)</c> à compter de
    /// <paramref name="dateEffet"/>. Ferme la version en vigueur du même
    /// triplet si elle existe (<c>DateFin</c> = veille de <paramref name="dateEffet"/>).
    /// Échoue explicitement si la rubrique, le critère ou le groupe référencés
    /// n'existent pas (<see cref="Error.NotFound"/>), ou si l'opérateur est
    /// invalide (<see cref="Error.Validation"/>).
    /// </summary>
    /// <returns>L'Id (généré) de la ligne créée.</returns>
    Task<Result<string>> DefinirRegleAsync(
        string rubriqueId,
        string critereId,
        string? groupeId,
        string operateur,
        string valeur,
        string dateEffet,
        string? source,
        DateTimeOffset creeLe,
        CancellationToken ct = default,
        IUnitOfWork? uow = null);

    /// <summary>
    /// Clôture une condition en vigueur (<c>DateFin</c> = <paramref name="dateFin"/>)
    /// sans la remplacer — suppression logique pure. Échoue si la condition
    /// n'existe pas (<see cref="Error.NotFound"/>) ou est déjà close
    /// (<see cref="Error.Conflict"/>).
    /// </summary>
    Task<Result<string>> CloreRegleAsync(
        string regleId,
        string dateFin,
        CancellationToken ct = default,
        IUnitOfWork? uow = null);
}
