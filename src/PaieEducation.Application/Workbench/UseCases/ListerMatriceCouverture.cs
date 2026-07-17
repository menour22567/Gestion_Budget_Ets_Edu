using PaieEducation.Domain.Common;
using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Use case Workbench (Phase 5, tâche 5, D11, J3I §5.5) : matrice de
/// couverture <c>corps × rubriques</c> — pour chaque couple, indique si une
/// règle d'éligibilité couvre ce corps pour cette rubrique
/// (<see cref="CelluleCouverture.Couverte"/>) et si elle est actuellement en
/// vigueur (<see cref="CelluleCouverture.Active"/>).
/// </summary>
/// <remarks>
/// Résout les conditions <c>GRADE</c> vers leur corps via <c>Grades.CorpsId</c>
/// (les données réelles, ex. ISSRP, référencent presque exclusivement
/// <c>GRADE</c>, jamais <c>CORPS</c> directement) ; les conditions <c>CORPS</c>
/// sont utilisées telles quelles. Seuls les opérateurs <c>=</c>/<c>IN</c> sont
/// résolus — <c>NOT_IN</c> (« tous sauf ceux-ci ») n'est pas représentable
/// dans une matrice statique et est ignoré (limite documentée, aucune donnée
/// réelle actuelle n'en dépend). Aucune couleur n'est calculée ici : la
/// 4e nuance du mockup (« Gris = non applicable ») relève de règles
/// d'affichage (Phase 6), pas de ce use case.
/// </remarks>
public sealed class ListerMatriceCouverture
{
    public sealed record Demande(string DatePaie);

    private readonly IWorkbenchReadRepository _workbench;

    public ListerMatriceCouverture(IWorkbenchReadRepository workbench)
        => _workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));

    public async Task<Result<IReadOnlyList<CelluleCouverture>>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        Guard.AgainstNullOrWhiteSpace(demande.DatePaie);

        var corps = await _workbench.ListerCorpsActifsAsync(ct);
        var rubriques = await _workbench.ListerRubriquesActivesAsync(ct);
        var grades = await _workbench.ListerGradesActifsAsync(ct);
        var conditions = await _workbench.ListerConditionsCorpsGradeAsync(ct);

        var corpsParGrade = grades.ToDictionary(g => g.GradeId, g => g.CorpsId, StringComparer.Ordinal);
        var corpsIdsValides = corps.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);

        // (CorpsId, RubriqueId) -> active (OR de toutes les conditions résolues sur ce couple).
        var couvertures = new Dictionary<(string CorpsId, string RubriqueId), bool>();

        foreach (var condition in conditions)
        {
            var active = EstActive(condition.Periode, demande.DatePaie);
            foreach (var valeur in ExtraireValeurs(condition))
            {
                var corpsId = condition.CritereId == "CORPS" ? valeur : corpsParGrade.GetValueOrDefault(valeur);
                if (corpsId is null || !corpsIdsValides.Contains(corpsId))
                    continue;

                var cle = (corpsId, condition.RubriqueId);
                couvertures[cle] = couvertures.TryGetValue(cle, out var dejaActive) ? dejaActive || active : active;
            }
        }

        var cellules = new List<CelluleCouverture>(corps.Count * rubriques.Count);
        foreach (var c in corps)
        {
            foreach (var r in rubriques)
            {
                var couverte = couvertures.TryGetValue((c.Id, r.Id), out var active);
                cellules.Add(new CelluleCouverture(c.Id, r.Id, couverte, active));
            }
        }

        return Result.Success<IReadOnlyList<CelluleCouverture>>(cellules);
    }

    private static bool EstActive(PeriodeReglementaire periode, string datePaie)
        => string.CompareOrdinal(periode.DateEffet, datePaie) <= 0
            && (periode.DateFin is null || string.CompareOrdinal(periode.DateFin, datePaie) >= 0);

    private static IEnumerable<string> ExtraireValeurs(ConditionEligibilite condition) => condition.Operateur switch
    {
        Operateur.Egal => [condition.Valeur],
        Operateur.In => condition.Valeur.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        _ => [],
    };
}

/// <summary>
/// Une cellule de la matrice de couverture (D11) : couple (corps, rubrique).
/// </summary>
/// <param name="Couverte">Vrai si au moins une condition <c>GRADE</c>/<c>CORPS</c> (active ou expirée) résout vers ce couple.</param>
/// <param name="Active">Vrai si au moins une des conditions résolues est en vigueur à la date demandée (faux si aucune, ou si <see cref="Couverte"/> est faux).</param>
public sealed record CelluleCouverture(string CorpsId, string RubriqueId, bool Couverte, bool Active);
