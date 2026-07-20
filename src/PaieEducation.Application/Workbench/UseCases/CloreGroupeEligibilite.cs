using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Chantier P6 (audit du 19/07/2026, éditeur DNF d'éligibilité) : clôture un
/// groupe DNF en vigueur (suppression logique, jamais de <c>DELETE</c> —
/// ADR-0008). Ne remplace jamais le groupe par une nouvelle version — pour
/// « réviser » un groupe, en créer un nouveau via
/// <see cref="DefinirGroupeEligibilite"/> puis clore l'ancien.
/// </summary>
public sealed class CloreGroupeEligibilite
{
    public sealed record Demande(string GroupeId, string DateFin);

    private readonly IGroupeEligibiliteRepository _groupes;

    public CloreGroupeEligibilite(IGroupeEligibiliteRepository groupes)
        => _groupes = groupes ?? throw new ArgumentNullException(nameof(groupes));

    public async Task<Result<string>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        Guard.AgainstNullOrWhiteSpace(demande.GroupeId);
        Guard.AgainstNullOrWhiteSpace(demande.DateFin);

        return await _groupes.CloreGroupeAsync(demande.GroupeId, demande.DateFin, ct);
    }
}
