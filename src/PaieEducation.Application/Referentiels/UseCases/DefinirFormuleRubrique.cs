using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Referentiels.UseCases;

/// <summary>
/// Use case C4.1 — définit une nouvelle version de formule pour une rubrique.
/// La formule est validée syntaxiquement par le <see cref="Domain.Calcul.Formules.FormulaParser"/>
/// avant persistance : une formule invalide est rejetée avec un message clair,
/// jamais une exception qui fuit. La version suit la continuité temporelle
/// (ferme la version courante à la veille, refuse date en double/non postérieure).
/// </summary>
public sealed class DefinirFormuleRubrique
{
    /// <summary>Données de définition d'une formule de rubrique.</summary>
    public sealed record Demande(string RubriqueId, string Expression, string DateEffet, int Ordre = 0, string? Source = null);

    private readonly IRubriqueRepository _rubriques;
    private readonly IClock _clock;

    public DefinirFormuleRubrique(IRubriqueRepository rubriques, IClock clock)
    {
        _rubriques = rubriques ?? throw new ArgumentNullException(nameof(rubriques));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<string>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        return await _rubriques.DefinirFormuleAsync(
            demande.RubriqueId, demande.Expression, demande.DateEffet, demande.Ordre,
            demande.Source, _clock.UtcNow, ct);
    }
}
