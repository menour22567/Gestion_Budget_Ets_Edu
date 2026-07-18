using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Fiche de consultation d'une rubrique (Phase 6, tâche 4, catalogue
/// Rubriques) — agrège Identité, Barème et Éligibilité en un seul
/// aller-retour (même patron que <see cref="ListerMatriceCouverture"/> :
/// plusieurs lectures consommées ensemble par un seul écran). Lecture
/// seule — aucun chemin d'écriture pour les barèmes/conditions ISSRP
/// n'existe encore (FormulaEditor/éditeurs, tâches 5-7, restent hors
/// périmètre).
/// </summary>
public sealed class ConsulterFicheRubrique
{
    public sealed record Demande(string RubriqueId, string DatePaie);

    public sealed record FicheRubrique(
        RubriqueDetail Detail,
        IReadOnlyList<BaremeValue> Baremes,
        IReadOnlyList<ConditionEligibilite> Conditions,
        IReadOnlyList<GroupeEligibilite> Groupes);

    private readonly IWorkbenchReadRepository _workbench;

    public ConsulterFicheRubrique(IWorkbenchReadRepository workbench)
        => _workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));

    public async Task<Result<FicheRubrique>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        Guard.AgainstNullOrWhiteSpace(demande.RubriqueId);
        Guard.AgainstNullOrWhiteSpace(demande.DatePaie);

        var detail = await _workbench.ObtenirRubriqueAsync(demande.RubriqueId, ct);
        if (detail is null)
            return Result.Failure<FicheRubrique>(Error.NotFound($"Rubrique '{demande.RubriqueId}' introuvable."));

        var baremes = await _workbench.ListerBaremesRubriqueAsync(demande.RubriqueId, ct);
        var conditions = await _workbench.ListerConditionsParRubriqueAsync(demande.RubriqueId, demande.DatePaie, ct);
        var groupes = await _workbench.ListerGroupesParRubriqueAsync(demande.RubriqueId, demande.DatePaie, ct);

        return Result.Success(new FicheRubrique(detail, baremes, conditions, groupes));
    }
}
