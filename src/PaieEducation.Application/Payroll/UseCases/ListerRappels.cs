using PaieEducation.Domain.Calcul.Rappels;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Shared.Results;

namespace PaieEducation.Application.Payroll.UseCases;

/// <summary>
/// Restitue les rappels déjà générés pour un bulletin validé (P9) — lecture
/// seule, enveloppe mince de <see cref="IRappelRepository.ListerAsync"/>,
/// même patron que <see cref="ConsulterBulletin"/>. Consommée par l'écran
/// « Consulter un bulletin » et par <c>ExporterBulletin</c> (Reporting).
/// </summary>
public sealed class ListerRappels
{
    public sealed record Demande(string AgentId, string DatePaie);

    private readonly IRappelRepository _rappels;

    public ListerRappels(IRappelRepository rappels)
        => _rappels = rappels ?? throw new ArgumentNullException(nameof(rappels));

    public Task<Result<IReadOnlyList<LigneRappel>>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        return _rappels.ListerAsync(demande.AgentId, demande.DatePaie, ct);
    }
}
