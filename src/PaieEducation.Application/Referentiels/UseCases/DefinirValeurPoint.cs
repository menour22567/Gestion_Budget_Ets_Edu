using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Referentiels.UseCases;

/// <summary>
/// Use case pilote (Phase 5, tâche 4, GérerRéférentiels — Q3) : définit une
/// nouvelle valeur du point indiciaire à compter d'une date d'effet.
/// </summary>
public sealed class DefinirValeurPoint
{
    /// <summary>Nouvelle valeur du point indiciaire à compter de <see cref="DateEffet"/>.</summary>
    public sealed record Demande(decimal Valeur, string DateEffet, string Version, string? Source = null);

    private readonly IGrilleIndiciaireRepository _grille;
    private readonly IClock _clock;

    public DefinirValeurPoint(IGrilleIndiciaireRepository grille, IClock clock)
    {
        _grille = grille ?? throw new ArgumentNullException(nameof(grille));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<string>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        Guard.AgainstNullOrWhiteSpace(demande.DateEffet);
        Guard.AgainstNullOrWhiteSpace(demande.Version);

        return await _grille.DefinirValeurPointAsync(
            demande.Valeur, demande.DateEffet, demande.Version, demande.Source, _clock.UtcNow, ct);
    }
}
