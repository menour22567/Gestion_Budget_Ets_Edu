using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Common;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Referentiels.UseCases;

/// <summary>
/// Use case pilote (Phase 5, tâche 4, GérerRéférentiels — Q3) : définit un
/// nouvel indice d'échelon à compter d'une date d'effet.
/// </summary>
public sealed class DefinirIndiceEchelon
{
    /// <summary>Nouvel indice pour <see cref="EchelonId"/> à compter de <see cref="DateEffet"/>.</summary>
    public sealed record Demande(string EchelonId, int Indice, string DateEffet, string Version, string? Source = null);

    private readonly IGrilleIndiciaireRepository _grille;
    private readonly IClock _clock;

    public DefinirIndiceEchelon(IGrilleIndiciaireRepository grille, IClock clock)
    {
        _grille = grille ?? throw new ArgumentNullException(nameof(grille));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<string>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        Guard.AgainstNullOrWhiteSpace(demande.EchelonId);
        Guard.AgainstNullOrWhiteSpace(demande.DateEffet);
        Guard.AgainstNullOrWhiteSpace(demande.Version);

        return await _grille.DefinirIndiceEchelonAsync(
            demande.EchelonId, demande.Indice, demande.DateEffet, demande.Version, demande.Source, _clock.UtcNow, ct);
    }
}
