using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Referentiels.UseCases;

/// <summary>
/// Use case pilote (Phase 5, tâche 4, GérerRéférentiels — Q3) : définit un
/// nouvel indice minimum de grille pour une catégorie à compter d'une date
/// d'effet.
/// </summary>
public sealed class DefinirIndiceMinGrille
{
    /// <summary>Nouvel indice minimum pour <see cref="CategorieId"/> à compter de <see cref="DateEffet"/>.</summary>
    public sealed record Demande(string CategorieId, int IndiceMin, string DateEffet, string Version, string? Source = null);

    private readonly IGrilleIndiciaireRepository _grille;
    private readonly IClock _clock;

    public DefinirIndiceMinGrille(IGrilleIndiciaireRepository grille, IClock clock)
    {
        _grille = grille ?? throw new ArgumentNullException(nameof(grille));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<string>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        Guard.AgainstNullOrWhiteSpace(demande.CategorieId);
        Guard.AgainstNullOrWhiteSpace(demande.DateEffet);
        Guard.AgainstNullOrWhiteSpace(demande.Version);

        return await _grille.DefinirIndiceMinAsync(
            demande.CategorieId, demande.IndiceMin, demande.DateEffet, demande.Version, demande.Source, _clock.UtcNow, ct);
    }
}
