using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Domain.Calcul.Repositories;

/// <summary>
/// Relit le snapshot d'un bulletin déjà validé. Port du Domain implémenté
/// par <c>Infrastructure.Repositories.Payroll.BulletinReadRepository</c>.
/// </summary>
public interface IBulletinReadRepository
{
    /// <summary>
    /// Renvoie le snapshot du bulletin validé de l'agent à la date de paie
    /// donnée. Échoue explicitement (<see cref="Error.NotFound"/>) si aucun
    /// bulletin n'a été validé pour cet agent à cette date.
    /// </summary>
    Task<Result<BulletinSnapshot>> ConsulterAsync(string agentId, string datePaie, CancellationToken ct = default);

    /// <summary>
    /// Compte le nombre de bulletins validés dont la <c>DatePaie</c> est
    /// comprise dans l'intervalle [<paramref name="periodeDebut"/> ;
    /// <paramref name="periodeFin"/>]. Utilisé par le simulateur d'impact
    /// (D8 / ADR-0007, J5L §3.3) pour calculer le nombre de bulletins qui
    /// seraient avertis (« BulletinsAvertis ») si une évolution réglementaire
    /// rétroactive était appliquée. <paramref name="periodeFin"/> peut être
    /// <c>null</c> pour « jusqu'à aujourd'hui » (période ouverte).
    /// </summary>
    Task<Result<int>> CompterPourPeriodeAsync(
        string periodeDebut, string? periodeFin, CancellationToken ct = default);
}
