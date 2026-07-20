using PaieEducation.Domain.Calcul.Rappels;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Domain.Calcul.Repositories;

/// <summary>
/// Persiste les <see cref="LigneRappel"/> générées par
/// <see cref="RappelCalculator"/> pour un bulletin déjà validé (<c>Rappels</c>,
/// V013). Port du Domain implémenté par
/// <c>Infrastructure.Repositories.Payroll.RappelRepository</c>.
/// </summary>
public interface IRappelRepository
{
    /// <summary>
    /// Indique si des rappels ont déjà été générés pour cet agent à cette
    /// date d'origine — garde d'idempotence utilisée par <c>GenererRappels</c>.
    /// </summary>
    Task<Result<bool>> ExisteAsync(string agentId, string datePaieOrigine, CancellationToken ct = default);

    /// <summary>
    /// Liste les rappels déjà générés pour cet agent à cette date d'origine
    /// (P9) — restitution dans le parcours bulletin (écran + export).
    /// Liste vide si aucun rappel n'a été généré.
    /// </summary>
    Task<Result<IReadOnlyList<LigneRappel>>> ListerAsync(string agentId, string datePaieOrigine, CancellationToken ct = default);

    Task<Result> EnregistrerAsync(
        string agentId,
        string datePaieOrigine,
        IReadOnlyList<LigneRappel> lignes,
        DateTimeOffset genereLe,
        CancellationToken ct = default);
}
