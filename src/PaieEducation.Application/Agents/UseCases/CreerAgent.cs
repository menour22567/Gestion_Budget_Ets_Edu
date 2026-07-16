using PaieEducation.Domain.Agents;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Domain.Common;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Agents.UseCases;

/// <summary>
/// Use case pilote (Phase 5, tâche 4) : crée un agent et sa carrière initiale.
/// </summary>
/// <remarks>
/// Valide les champs texte requis et les valeurs énumérées (<c>Sexe</c>,
/// <c>SituationFamiliale</c>, <c>TypeContrat</c> — mêmes listes que les
/// contraintes <c>CHECK</c> V011) avant d'appeler <see cref="IAgentRepository"/>,
/// pour échouer explicitement (<see cref="Error.Validation"/>) plutôt que de
/// laisser remonter une exception SQLite. Les FK (Grade/Catégorie/Échelon/
/// Fonction/Établissement) ne sont pas vérifiées ici — hors périmètre V1,
/// cohérent avec le reste des repositories existants.
/// </remarks>
public sealed class CreerAgent
{
    private static readonly string[] SexesValides = ["M", "F"];
    private static readonly string[] SituationsValides = ["CELIBATAIRE", "MARIE", "DIVORCE", "VEUF"];
    private static readonly string[] TypesContratValides = ["STATUTAIRE", "CONTRACTUEL"];

    private readonly IAgentRepository _agents;
    private readonly IClock _clock;

    public CreerAgent(IAgentRepository agents, IClock clock)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
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

        if (!SexesValides.Contains(demande.Sexe))
            return Result.Failure<string>(Error.Validation($"Sexe invalide : '{demande.Sexe}' (attendu M ou F)."));
        if (!SituationsValides.Contains(demande.SituationFamiliale))
            return Result.Failure<string>(Error.Validation(
                $"Situation familiale invalide : '{demande.SituationFamiliale}'."));
        if (!TypesContratValides.Contains(demande.TypeContrat))
            return Result.Failure<string>(Error.Validation($"Type de contrat invalide : '{demande.TypeContrat}'."));

        return await _agents.CreerAsync(demande, _clock.UtcNow, ct);
    }
}
