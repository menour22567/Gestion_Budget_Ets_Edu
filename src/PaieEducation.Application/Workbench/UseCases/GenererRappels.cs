using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Rappels;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Domain.Common;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Génère les lignes de rappel (D9) pour UN bulletin déjà validé, en
/// recalculant « à droit constant actuel » à la même date de paie — la
/// résolution point-in-time déjà en place (grille indiciaire, barèmes,
/// conditions) fait automatiquement remonter une évolution réglementaire
/// rétroactive sans logique de détection dédiée.
/// </summary>
/// <remarks>
/// Portée volontairement réduite (voir mémoire phase5-genererrappels) :
/// un agent + un bulletin à la fois (pas de balayage multi-agents par
/// période — cette énumération n'existe pas), et persistance dans une
/// table <c>Rappels</c> dédiée plutôt qu'en ligne d'un futur bulletin
/// (le moteur de calcul n'a aucune notion de « rappels en attente »).
/// </remarks>
public sealed class GenererRappels
{
    private readonly IBulletinReadRepository _bulletinsLecture;
    private readonly IRappelRepository _rappels;
    private readonly IAgentCarriereRepository _agents;
    private readonly IVariableRepository _variables;
    private readonly IPayrollReadRepository _payroll;
    private readonly IClock _clock;

    public GenererRappels(
        IBulletinReadRepository bulletinsLecture,
        IRappelRepository rappels,
        IAgentCarriereRepository agents,
        IVariableRepository variables,
        IPayrollReadRepository payroll,
        IClock clock)
    {
        _bulletinsLecture = bulletinsLecture ?? throw new ArgumentNullException(nameof(bulletinsLecture));
        _rappels = rappels ?? throw new ArgumentNullException(nameof(rappels));
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _variables = variables ?? throw new ArgumentNullException(nameof(variables));
        _payroll = payroll ?? throw new ArgumentNullException(nameof(payroll));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<IReadOnlyList<LigneRappel>>> ExecuterAsync(
        CalculerBulletin.Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);

        var ancien = await _bulletinsLecture.ConsulterAsync(demande.AgentId, demande.DatePaie, ct);
        if (ancien.IsFailure) return Result.Failure<IReadOnlyList<LigneRappel>>(ancien.Error);

        var dejaGenere = await _rappels.ExisteAsync(demande.AgentId, demande.DatePaie, ct);
        if (dejaGenere.IsFailure) return Result.Failure<IReadOnlyList<LigneRappel>>(dejaGenere.Error);
        if (dejaGenere.Value)
            return Result.Failure<IReadOnlyList<LigneRappel>>(Error.Conflict(
                $"Des rappels ont déjà été générés pour l'agent '{demande.AgentId}' à la date {demande.DatePaie}."));

        var agent = await _agents.ResoudreAsync(demande.AgentId, demande.DatePaie, ct);
        if (agent.IsFailure) return Result.Failure<IReadOnlyList<LigneRappel>>(agent.Error);

        var variables = await _variables.ResoudreAsync(agent.Value, demande.DatePaie, ct);
        if (variables.IsFailure) return Result.Failure<IReadOnlyList<LigneRappel>>(variables.Error);

        var input = await _payroll.ChargerAsync(
            agent.Value, demande.DatePaie, variables.Value, demande.SourcesValeur, demande.ClesBareme,
            demande.Profil, ct);
        if (input.IsFailure) return Result.Failure<IReadOnlyList<LigneRappel>>(input.Error);

        var nouveau = new CalculationPipeline(new ArrondiService(ModeArrondi.DinarPlusProche)).Calculer(input.Value);
        if (nouveau.IsFailure) return Result.Failure<IReadOnlyList<LigneRappel>>(nouveau.Error);

        var lignes = new RappelCalculator().Calculer(ancien.Value, nouveau.Value);
        if (lignes.Count == 0)
            return Result.Success<IReadOnlyList<LigneRappel>>([]);

        var maintenant = _clock.UtcNow;
        var enregistre = await _rappels.EnregistrerAsync(demande.AgentId, demande.DatePaie, lignes, maintenant, ct);
        if (enregistre.IsFailure) return Result.Failure<IReadOnlyList<LigneRappel>>(enregistre.Error);

        return Result.Success<IReadOnlyList<LigneRappel>>(lignes);
    }
}
