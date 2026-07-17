using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Common;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Clone la valeur du point indiciaire en vigueur vers une nouvelle période
/// réglementaire (mode « Duplication », J3I §7.4) — même taux, nouvelle
/// date d'effet ; l'utilisateur ajuste ensuite les paramètres si besoin via
/// <c>DefinirValeurPoint</c>. Portée limitée à <c>ValeurPoint</c> (même
/// périmètre que <c>GérerRéférentiels</c>, Phase 5 tâche 4) — voir mémoire
/// phase5-dupliquerversion.
/// </summary>
public sealed class DupliquerVersion
{
    public sealed record Demande(string NouvelleDateEffet, string Version, string? Source = null);

    private readonly IGrilleIndiciaireRepository _grille;
    private readonly IClock _clock;

    public DupliquerVersion(IGrilleIndiciaireRepository grille, IClock clock)
    {
        _grille = grille ?? throw new ArgumentNullException(nameof(grille));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<string>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        Guard.AgainstNullOrWhiteSpace(demande.NouvelleDateEffet);
        Guard.AgainstNullOrWhiteSpace(demande.Version);

        return await _grille.DupliquerValeurPointAsync(
            demande.NouvelleDateEffet, demande.Version, demande.Source, _clock.UtcNow, ct);
    }
}
