using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Chantier P6 (audit du 19/07/2026, éditeur DNF d'éligibilité) : définit une
/// nouvelle condition d'éligibilité (<c>ReglesEligibilite</c>) pour le
/// triplet <c>(RubriqueId, CritereId, GroupeId)</c> à compter d'une date
/// d'effet — crée si aucune version n'existait, verse une nouvelle version
/// sinon (« créée/versionnée », critère d'acceptation du plan P6).
/// <see cref="Demande.GroupeId"/> <c>null</c> = condition commune (ET plat).
/// </summary>
public sealed class DefinirRegleEligibilite
{
    public sealed record Demande(
        string RubriqueId,
        string CritereId,
        string? GroupeId,
        string Operateur,
        string Valeur,
        string DateEffet,
        string? Source = null);

    private readonly IRegleEligibiliteRepository _regles;
    private readonly IClock _clock;

    public DefinirRegleEligibilite(IRegleEligibiliteRepository regles, IClock clock)
    {
        _regles = regles ?? throw new ArgumentNullException(nameof(regles));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<string>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        Guard.AgainstNullOrWhiteSpace(demande.RubriqueId);
        Guard.AgainstNullOrWhiteSpace(demande.CritereId);
        Guard.AgainstNullOrWhiteSpace(demande.Operateur);
        Guard.AgainstNullOrWhiteSpace(demande.Valeur);
        Guard.AgainstNullOrWhiteSpace(demande.DateEffet);

        return await _regles.DefinirRegleAsync(
            demande.RubriqueId, demande.CritereId, demande.GroupeId, demande.Operateur, demande.Valeur,
            demande.DateEffet, demande.Source, _clock.UtcNow, ct);
    }
}
