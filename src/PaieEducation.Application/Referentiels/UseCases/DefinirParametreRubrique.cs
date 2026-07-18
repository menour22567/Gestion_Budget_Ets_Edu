using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Referentiels.UseCases;

/// <summary>
/// Use case C4.1 — définit un nouveau paramètre versionné pour une rubrique
/// (taux, seuil, forfait pilotés en base). Même continuité temporelle que les
/// formules : une nouvelle version ferme la précédente, jamais de recouvrement.
/// </summary>
public sealed class DefinirParametreRubrique
{
    /// <summary>Données de définition d'un paramètre de rubrique.</summary>
    public sealed record Demande(string RubriqueId, string Cle, string Valeur, string DateEffet, string? Source = null);

    private readonly IRubriqueRepository _rubriques;
    private readonly IClock _clock;

    public DefinirParametreRubrique(IRubriqueRepository rubriques, IClock clock)
    {
        _rubriques = rubriques ?? throw new ArgumentNullException(nameof(rubriques));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<string>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        return await _rubriques.DefinirParametreAsync(
            demande.RubriqueId, demande.Cle, demande.Valeur, demande.DateEffet,
            demande.Source, _clock.UtcNow, ct);
    }
}
