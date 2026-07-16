using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Domain.Common;

namespace PaieEducation.Application.Payroll.UseCases;

/// <summary>
/// Use case pilote (Phase 5, tâche 4) : calcule le bulletin d'un agent à une
/// date de paie donnée. Lecture seule — n'écrit rien en base (la persistance
/// du bulletin validé relève d'un use case distinct, <c>ValiderBulletin</c>).
/// </summary>
/// <remarks>
/// Orchestre les trois ports du sous-arbre Calcul
/// (<see cref="IAgentCarriereRepository"/>, <see cref="IVariableRepository"/>,
/// <see cref="IPayrollReadRepository"/>) puis <see cref="CalculationPipeline"/>
/// (service Domain pur, instancié directement — pas d'I/O donc pas de port).
/// <see cref="Demande.SourcesValeur"/> et <see cref="Demande.ClesBareme"/>
/// restent fournis par l'appelant : leur auto-résolution (notation agent,
/// clés de barème depuis la carrière) est une dette distincte.
/// </remarks>
public sealed class CalculerBulletin
{
    /// <summary>Demande de calcul d'un bulletin pour un agent à une date de paie.</summary>
    public sealed record Demande(
        string AgentId,
        string DatePaie,
        IReadOnlyDictionary<string, decimal> SourcesValeur,
        IReadOnlyDictionary<string, string> ClesBareme,
        ProfilFiscal Profil);

    private readonly IAgentCarriereRepository _agents;
    private readonly IVariableRepository _variables;
    private readonly IPayrollReadRepository _payroll;

    public CalculerBulletin(
        IAgentCarriereRepository agents, IVariableRepository variables, IPayrollReadRepository payroll)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _variables = variables ?? throw new ArgumentNullException(nameof(variables));
        _payroll = payroll ?? throw new ArgumentNullException(nameof(payroll));
    }

    public async Task<Result<Bulletin>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);

        var agent = await _agents.ResoudreAsync(demande.AgentId, demande.DatePaie, ct);
        if (agent.IsFailure)
            return Result.Failure<Bulletin>(agent.Error);

        var variables = await _variables.ResoudreAsync(agent.Value, demande.DatePaie, ct);
        if (variables.IsFailure)
            return Result.Failure<Bulletin>(variables.Error);

        var input = await _payroll.ChargerAsync(
            agent.Value, demande.DatePaie, variables.Value, demande.SourcesValeur, demande.ClesBareme,
            demande.Profil, ct);
        if (input.IsFailure)
            return Result.Failure<Bulletin>(input.Error);

        return new CalculationPipeline(new ArrondiService(ModeArrondi.DinarPlusProche)).Calculer(input.Value);
    }
}
