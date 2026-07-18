using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Referentiels.UseCases;

/// <summary>
/// Use case C4.1 — crée ou met à jour les métadonnées d'une rubrique. La rubrique
/// est l'identité stable d'un élément de paie (IEP reste IEP) ; ce qui change dans
/// le temps (formule, paramètres, barèmes) vit dans les tables filles versionnées.
/// </summary>
public sealed class DefinirRubrique
{
    /// <summary>Données de définition d'une rubrique.</summary>
    public sealed record Demande(
        string Id, string Libelle, string Nature, string BaseCalcul, string Periodicite,
        string? PeriodiciteVersement, int OrdreCalcul, bool EstImposable, bool EstCotisable,
        string Description, bool EstAffectableManuellement, bool OccurrencesMultiples,
        string? SourceValeurId = null, string? Source = null);

    private readonly IRubriqueRepository _rubriques;
    private readonly IClock _clock;

    public DefinirRubrique(IRubriqueRepository rubriques, IClock clock)
    {
        _rubriques = rubriques ?? throw new ArgumentNullException(nameof(rubriques));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<string>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        return await _rubriques.DefinirRubriqueAsync(
            demande.Id, demande.Libelle, demande.Nature, demande.BaseCalcul, demande.Periodicite,
            demande.PeriodiciteVersement, demande.OrdreCalcul, demande.EstImposable, demande.EstCotisable,
            demande.Description, demande.EstAffectableManuellement, demande.OccurrencesMultiples,
            demande.SourceValeurId, demande.Source, _clock.UtcNow, ct);
    }
}
