using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Chantier P6 (audit du 19/07/2026, éditeur DNF d'éligibilité) : clôture une
/// condition d'éligibilité en vigueur sans la remplacer — suppression
/// logique pure (jamais de <c>DELETE</c> — ADR-0008). Complète
/// <see cref="DefinirRegleEligibilite"/>, qui ferme-et-remplace : les deux
/// use cases couvrent ensemble « créée/fermée/versionnée » (critère
/// d'acceptation du plan P6).
/// </summary>
public sealed class CloreRegleEligibilite
{
    public sealed record Demande(string RegleId, string DateFin);

    private readonly IRegleEligibiliteRepository _regles;

    public CloreRegleEligibilite(IRegleEligibiliteRepository regles)
        => _regles = regles ?? throw new ArgumentNullException(nameof(regles));

    public async Task<Result<string>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        Guard.AgainstNullOrWhiteSpace(demande.RegleId);
        Guard.AgainstNullOrWhiteSpace(demande.DateFin);

        return await _regles.CloreRegleAsync(demande.RegleId, demande.DateFin, ct);
    }
}
