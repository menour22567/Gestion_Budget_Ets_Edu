using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Domain.Common;

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
}
