using System.Globalization;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Payroll.UseCases;

/// <summary>
/// Use case pilote (Phase 5, tâche 4) : calcule le bulletin d'un agent et le
/// **fige** (Snapshot Engine, RM-105) avant de le persister — c'est le
/// prérequis d'ADR-0008 pour tout futur rappel.
/// </summary>
/// <remarks>
/// Réutilise l'orchestration de <see cref="CalculerBulletin.ResoudreAsync"/>
/// (auto-résolution des entrées C2.2/C2.3 + arrondi paramétré C2.1) pour
/// obtenir à la fois le <c>PayrollInput</c> (nécessaire au snapshot) et le
/// <see cref="Bulletin"/>. N'écrit rien si un bulletin existe déjà pour cet
/// agent à cette date (<see cref="IBulletinRepository.ValiderAsync"/> échoue
/// explicitement — ADR-0008, un bulletin validé n'est jamais réécrit). Hors
/// périmètre : la transition d'état des <c>Periodes</c> et le rejet sur
/// période clôturée.
/// </remarks>
public sealed class ValiderBulletin
{
    private readonly CalculerBulletin _calculer;
    private readonly IBulletinRepository _bulletins;
    private readonly IClock _clock;

    public ValiderBulletin(CalculerBulletin calculer, IBulletinRepository bulletins, IClock clock)
    {
        _calculer = calculer ?? throw new ArgumentNullException(nameof(calculer));
        _bulletins = bulletins ?? throw new ArgumentNullException(nameof(bulletins));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<string>> ExecuterAsync(CalculerBulletin.Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);

        var calcule = await _calculer.ResoudreAsync(demande, ct);
        if (calcule.IsFailure)
            return Result.Failure<string>(calcule.Error);

        var (input, bulletin) = calcule.Value;

        var maintenant = _clock.UtcNow;
        var horodatage = maintenant.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var snapshot = new SnapshotEngine().Capturer(input, bulletin, horodatage);

        return await _bulletins.ValiderAsync(demande.AgentId, snapshot, maintenant, ct);
    }
}
