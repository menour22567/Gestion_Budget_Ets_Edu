using PaieEducation.Domain.Agents;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Agents.UseCases;

/// <summary>
/// Use case pilote (Phase 5, tâche 4) : crée un agent et sa carrière initiale.
/// </summary>
/// <remarks>
/// Valide les champs texte requis et les valeurs énumérées (<c>Sexe</c>,
/// <c>SituationFamiliale</c>, <c>TypeContrat</c> — listes issues des tables
/// <c>TypesSexe</c>, <c>SituationsFamiliales</c>, <c>TypesContrat</c>) avant
/// d'appeler <see cref="IAgentRepository"/>, pour échouer explicitement
/// (<see cref="Error.Validation"/>) plutôt que de laisser remonter une
/// exception SQLite. Les FK (Grade/Catégorie/Échelon/Fonction/Établissement)
/// ne sont pas vérifiées ici — hors périmètre V1, cohérent avec le reste des
/// repositories existants.
/// </remarks>
public sealed class CreerAgent
{
    private readonly IAgentRepository _agents;
    private readonly IAgentReadRepository _agentRead;
    private readonly IClock _clock;

    public CreerAgent(IAgentRepository agents, IAgentReadRepository agentRead, IClock clock)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _agentRead = agentRead ?? throw new ArgumentNullException(nameof(agentRead));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<string>> ExecuterAsync(NouvelAgent demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        Guard.AgainstNullOrWhiteSpace(demande.Matricule);
        Guard.AgainstNullOrWhiteSpace(demande.Nom);
        Guard.AgainstNullOrWhiteSpace(demande.Prenom);
        Guard.AgainstNullOrWhiteSpace(demande.DateNaissance);
        Guard.AgainstNullOrWhiteSpace(demande.DateRecrutement);
        Guard.AgainstNullOrWhiteSpace(demande.GradeId);
        Guard.AgainstNullOrWhiteSpace(demande.CategorieId);
        Guard.AgainstNullOrWhiteSpace(demande.EchelonId);

        var sexes = await _agentRead.ListerSexesAsync(ct);
        if (sexes.IsFailure) return Result.Failure<string>(sexes.Error);
        if (!sexes.Value.Any(s => s.Id == demande.Sexe))
            return Result.Failure<string>(Error.Validation($"Sexe invalide : '{demande.Sexe}'."));

        var situations = await _agentRead.ListerSituationsFamilialesAsync(ct);
        if (situations.IsFailure) return Result.Failure<string>(situations.Error);
        if (!situations.Value.Any(s => s.Id == demande.SituationFamiliale))
            return Result.Failure<string>(Error.Validation(
                $"Situation familiale invalide : '{demande.SituationFamiliale}'."));

        var contrats = await _agentRead.ListerTypesContratAsync(ct);
        if (contrats.IsFailure) return Result.Failure<string>(contrats.Error);
        if (!contrats.Value.Any(c => c.Id == demande.TypeContrat))
            return Result.Failure<string>(Error.Validation($"Type de contrat invalide : '{demande.TypeContrat}'."));

        return await _agents.CreerAsync(demande, _clock.UtcNow, ct);
    }
}
