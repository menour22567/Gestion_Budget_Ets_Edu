using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.ValueObjects;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Application.Referentiels.UseCases;

/// <summary>
/// Charge la nomenclature (Grades/Catégories/Échelons) nécessaire pour
/// peupler les sélecteurs d'écran (<c>CréerAgent</c>, <c>Grille
/// indiciaire</c>) — regroupée en un seul aller-retour car ces 3 lectures
/// n'ont pas d'intention métier distincte, elles servent toujours ensemble
/// à peupler un écran (même patron d'agrégation que
/// <c>ListerMatriceCouverture</c>).
/// </summary>
public sealed class ListerReferentiels
{
    public sealed record ReferentielsDisponibles(
        IReadOnlyList<ReferentielItem> Grades,
        IReadOnlyList<ReferentielItem> Categories,
        IReadOnlyList<ReferentielItem> Echelons);

    private readonly IReferentielReadRepository _referentiels;

    public ListerReferentiels(IReferentielReadRepository referentiels)
        => _referentiels = referentiels ?? throw new ArgumentNullException(nameof(referentiels));

    public async Task<Result<ReferentielsDisponibles>> ExecuterAsync(CancellationToken ct = default)
    {
        var grades = await _referentiels.ListerGradesAsync(ct);
        if (grades.IsFailure) return Result.Failure<ReferentielsDisponibles>(grades.Error);

        var categories = await _referentiels.ListerCategoriesAsync(ct);
        if (categories.IsFailure) return Result.Failure<ReferentielsDisponibles>(categories.Error);

        var echelons = await _referentiels.ListerEchelonsAsync(ct);
        if (echelons.IsFailure) return Result.Failure<ReferentielsDisponibles>(echelons.Error);

        return Result.Success(new ReferentielsDisponibles(grades.Value, categories.Value, echelons.Value));
    }
}
