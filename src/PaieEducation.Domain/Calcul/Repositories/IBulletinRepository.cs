using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Domain.Common;

namespace PaieEducation.Domain.Calcul.Repositories;

/// <summary>
/// Persiste le snapshot d'un bulletin validé (RM-105, ADR-0008). Port du
/// Domain implémenté par <c>Infrastructure.Repositories.Payroll.BulletinRepository</c>.
/// </summary>
public interface IBulletinRepository
{
    /// <summary>
    /// Persiste <paramref name="snapshot"/> pour l'agent <paramref name="agentId"/>
    /// (la date de paie est lue depuis <c>snapshot.Input.DatePaie</c> —
    /// <c>AgentContext</c> ne porte volontairement pas l'Id agent, cf. sa
    /// documentation). Échoue explicitement (<see cref="Error.Conflict"/>) si
    /// un bulletin existe déjà pour cet agent à cette date de paie — un
    /// bulletin validé n'est jamais réécrit (ADR-0008), jamais d'exception
    /// pour ce cas métier plausible.
    /// </summary>
    /// <returns>L'Id (GUID) du bulletin persisté, en cas de succès.</returns>
    Task<Result<string>> ValiderAsync(
        string agentId, BulletinSnapshot snapshot, DateTimeOffset valideLe, CancellationToken ct = default);
}
