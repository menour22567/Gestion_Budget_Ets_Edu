using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Application.Payroll.UseCases;

/// <summary>
/// Use case pilote (Phase 5, tâche 4) : consulte le bulletin déjà validé
/// d'un agent à une date de paie. Lecture seule — projette
/// <see cref="Bulletin"/> depuis le snapshot persisté par
/// <see cref="ValiderBulletin"/> ; ne recalcule jamais (ADR-0008, le
/// snapshot est la seule source de vérité une fois le bulletin validé).
/// </summary>
public sealed class ConsulterBulletin
{
    /// <summary>Demande de consultation d'un bulletin validé.</summary>
    public sealed record Demande(string AgentId, string DatePaie);

    private readonly IBulletinReadRepository _bulletins;

    public ConsulterBulletin(IBulletinReadRepository bulletins)
        => _bulletins = bulletins ?? throw new ArgumentNullException(nameof(bulletins));

    public async Task<Result<Bulletin>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);

        var snapshot = await _bulletins.ConsulterAsync(demande.AgentId, demande.DatePaie, ct);
        return snapshot.IsFailure
            ? Result.Failure<Bulletin>(snapshot.Error)
            : Result.Success(snapshot.Value.Resultat);
    }
}
