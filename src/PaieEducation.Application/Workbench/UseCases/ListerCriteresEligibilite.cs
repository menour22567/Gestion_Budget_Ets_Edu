using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Shared.Results;

namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Chantier P6 (audit du 19/07/2026, éditeur DNF d'éligibilité) : liste les
/// critères d'éligibilité actifs — alimente le sélecteur de critère de
/// l'éditeur DNF (convention zéro-hardcoding : pas de saisie libre de
/// <c>CritereId</c>). Enveloppe mince de
/// <see cref="IWorkbenchReadRepository.ListerCriteresParIdAsync"/>, déjà
/// utilisée par l'évaluateur.
/// </summary>
public sealed class ListerCriteresEligibilite
{
    private readonly IWorkbenchReadRepository _workbench;

    public ListerCriteresEligibilite(IWorkbenchReadRepository workbench)
        => _workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));

    public async Task<Result<IReadOnlyList<CritereEligibilite>>> ExecuterAsync(CancellationToken ct = default)
    {
        var criteres = await _workbench.ListerCriteresParIdAsync(ct);
        return Result.Success<IReadOnlyList<CritereEligibilite>>(
            [.. criteres.Values.OrderBy(c => c.Id, StringComparer.Ordinal)]);
    }
}
