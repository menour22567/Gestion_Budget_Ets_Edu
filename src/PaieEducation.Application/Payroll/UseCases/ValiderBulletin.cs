using System.Globalization;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Domain.Common;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Payroll.UseCases;

/// <summary>
/// Use case pilote (Phase 5, tâche 4) : calcule le bulletin d'un agent et le
/// **fige** (Snapshot Engine, RM-105) avant de le persister — c'est le
/// prérequis d'ADR-0008 pour tout futur rappel.
/// </summary>
/// <remarks>
/// Même orchestration de lecture que <see cref="CalculerBulletin"/>
/// (dupliquée plutôt que factorisée : celui-ci ne renvoie que
/// <see cref="Bulletin"/>, pas le <c>PayrollInput</c> nécessaire au
/// snapshot). Réutilise <see cref="CalculerBulletin.Demande"/> tel quel.
/// N'écrit rien si un bulletin existe déjà pour cet agent à cette date
/// (<see cref="IBulletinRepository.ValiderAsync"/> échoue explicitement —
/// ADR-0008, un bulletin validé n'est jamais réécrit). Hors périmètre : la
/// transition d'état des <c>Periodes</c> et le rejet sur période clôturée.
/// </remarks>
public sealed class ValiderBulletin
{
    private readonly IAgentCarriereRepository _agents;
    private readonly IVariableRepository _variables;
    private readonly IPayrollReadRepository _payroll;
    private readonly IBulletinRepository _bulletins;
    private readonly IClock _clock;

    public ValiderBulletin(
        IAgentCarriereRepository agents, IVariableRepository variables, IPayrollReadRepository payroll,
        IBulletinRepository bulletins, IClock clock)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _variables = variables ?? throw new ArgumentNullException(nameof(variables));
        _payroll = payroll ?? throw new ArgumentNullException(nameof(payroll));
        _bulletins = bulletins ?? throw new ArgumentNullException(nameof(bulletins));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<string>> ExecuterAsync(CalculerBulletin.Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);

        var agent = await _agents.ResoudreAsync(demande.AgentId, demande.DatePaie, ct);
        if (agent.IsFailure)
            return Result.Failure<string>(agent.Error);

        var variables = await _variables.ResoudreAsync(agent.Value, demande.DatePaie, ct);
        if (variables.IsFailure)
            return Result.Failure<string>(variables.Error);

        var input = await _payroll.ChargerAsync(
            agent.Value, demande.DatePaie, variables.Value, demande.SourcesValeur, demande.ClesBareme,
            demande.Profil, ct);
        if (input.IsFailure)
            return Result.Failure<string>(input.Error);

        var bulletin = new CalculationPipeline(new ArrondiService(ModeArrondi.DinarPlusProche)).Calculer(input.Value);
        if (bulletin.IsFailure)
            return Result.Failure<string>(bulletin.Error);

        var maintenant = _clock.UtcNow;
        var horodatage = maintenant.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var snapshot = new SnapshotEngine().Capturer(input.Value, bulletin.Value, horodatage);

        return await _bulletins.ValiderAsync(demande.AgentId, snapshot, maintenant, ct);
    }
}
