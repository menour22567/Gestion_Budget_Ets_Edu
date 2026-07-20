using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Chantier P6 (audit du 19/07/2026, éditeur DNF d'éligibilité) : crée un
/// nouveau groupe DNF (<c>GroupesEligibilite</c>). Même patron que
/// <see cref="DefinirValeurBareme"/> — enveloppe mince du port d'écriture.
/// </summary>
/// <remarks>
/// <see cref="Demande.CreatedBy"/> : aucun mécanisme d'identité utilisateur
/// n'existe en V1 (dette documentée, décision P17/Q12 en attente) — la valeur
/// fournie par l'appelant est enregistrée telle quelle (acteur déclaratif).
/// </remarks>
public sealed class DefinirGroupeEligibilite
{
    public sealed record Demande(
        string GroupeId,
        string RubriqueId,
        string Severite,
        string? MessageId,
        int Priorite,
        string DateEffet,
        string? DateFin,
        string? Source,
        string CreatedBy);

    private readonly IGroupeEligibiliteRepository _groupes;
    private readonly IClock _clock;

    public DefinirGroupeEligibilite(IGroupeEligibiliteRepository groupes, IClock clock)
    {
        _groupes = groupes ?? throw new ArgumentNullException(nameof(groupes));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<string>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        Guard.AgainstNullOrWhiteSpace(demande.GroupeId);
        Guard.AgainstNullOrWhiteSpace(demande.RubriqueId);
        Guard.AgainstNullOrWhiteSpace(demande.Severite);
        Guard.AgainstNullOrWhiteSpace(demande.DateEffet);
        Guard.AgainstNullOrWhiteSpace(demande.CreatedBy);

        return await _groupes.DefinirGroupeAsync(
            demande.GroupeId, demande.RubriqueId, demande.Severite, demande.MessageId, demande.Priorite,
            demande.DateEffet, demande.DateFin, demande.Source, demande.CreatedBy, _clock.UtcNow, ct);
    }
}
