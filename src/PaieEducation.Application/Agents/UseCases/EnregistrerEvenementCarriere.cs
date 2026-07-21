using PaieEducation.Domain.Agents;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Agents.UseCases;

/// <summary>
/// Enregistre un nouvel événement de carrière (avancement d'échelon,
/// promotion, mutation) pour un agent existant (chantier « gestion des
/// agents »). Valide <c>TypeContrat</c> contre la nomenclature (même contrôle
/// que <see cref="CreerAgent"/>) ; les FK Grade/Catégorie/Échelon/Fonction/
/// Établissement ne sont pas vérifiées ici — même périmètre assumé que
/// <see cref="CreerAgent"/> (hors V1). La continuité temporelle (date d'effet
/// postérieure à la carrière en vigueur, fermeture de la précédente) est
/// appliquée par <see cref="IAgentRepository.EnregistrerEvenementCarriereAsync"/>.
/// </summary>
public sealed class EnregistrerEvenementCarriere
{
    private readonly IAgentRepository _agents;
    private readonly IAgentReadRepository _agentRead;
    private readonly IClock _clock;

    public EnregistrerEvenementCarriere(IAgentRepository agents, IAgentReadRepository agentRead, IClock clock)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _agentRead = agentRead ?? throw new ArgumentNullException(nameof(agentRead));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<string>> ExecuterAsync(EvenementCarriere demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        Guard.AgainstNullOrWhiteSpace(demande.AgentId);
        Guard.AgainstNullOrWhiteSpace(demande.GradeId);
        Guard.AgainstNullOrWhiteSpace(demande.CategorieId);
        Guard.AgainstNullOrWhiteSpace(demande.EchelonId);
        Guard.AgainstNullOrWhiteSpace(demande.DateEffet);
        Guard.AgainstNullOrWhiteSpace(demande.Motif);

        var contrats = await _agentRead.ListerTypesContratAsync(ct);
        if (contrats.IsFailure) return Result.Failure<string>(contrats.Error);
        if (!contrats.Value.Any(c => c.Id == demande.TypeContrat))
            return Result.Failure<string>(Error.Validation($"Type de contrat invalide : '{demande.TypeContrat}'."));

        return await _agents.EnregistrerEvenementCarriereAsync(demande, _clock.UtcNow, ct);
    }
}
