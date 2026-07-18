using PaieEducation.Application.Payroll.UseCases;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Rappels;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
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
/// Réutilise <see cref="CalculerBulletin.ResoudreAsync"/> pour l'orchestration
/// de calcul (auto-résolution C2.2/C2.3 + arrondi paramétré C2.1).
/// </remarks>
public sealed class GenererRappels
{
    private readonly IBulletinReadRepository _bulletinsLecture;
    private readonly IRappelRepository _rappels;
    private readonly CalculerBulletin _calculer;
    private readonly IClock _clock;

    public GenererRappels(
        IBulletinReadRepository bulletinsLecture,
        IRappelRepository rappels,
        CalculerBulletin calculer,
        IClock clock)
    {
        _bulletinsLecture = bulletinsLecture ?? throw new ArgumentNullException(nameof(bulletinsLecture));
        _rappels = rappels ?? throw new ArgumentNullException(nameof(rappels));
        _calculer = calculer ?? throw new ArgumentNullException(nameof(calculer));
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

        // Orchestration de calcul partagée (auto-résolution C2.2/C2.3 + arrondi
        // paramétré C2.1).
        var calcule = await _calculer.ResoudreAsync(demande, ct);
        if (calcule.IsFailure) return Result.Failure<IReadOnlyList<LigneRappel>>(calcule.Error);
        var nouveau = calcule.Value.Bulletin;

        var lignes = new RappelCalculator().Calculer(ancien.Value, nouveau);
        if (lignes.Count == 0)
            return Result.Success<IReadOnlyList<LigneRappel>>([]);

        var maintenant = _clock.UtcNow;
        var enregistre = await _rappels.EnregistrerAsync(demande.AgentId, demande.DatePaie, lignes, maintenant, ct);
        if (enregistre.IsFailure) return Result.Failure<IReadOnlyList<LigneRappel>>(enregistre.Error);

        return Result.Success<IReadOnlyList<LigneRappel>>(lignes);
    }
}
