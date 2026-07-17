using PaieEducation.Domain.Calcul.ValueObjects;
using PaieEducation.Domain.Common;

namespace PaieEducation.Domain.Calcul.Repositories;

/// <summary>
/// Liste la nomenclature (<c>Grades</c>, <c>Categories</c>, <c>Echelons</c>,
/// V002) pour peupler des sélecteurs UI — symétrique en lecture des tables
/// consommées par <see cref="IAgentCarriereRepository"/>/<see cref="IVariableRepository"/>.
/// Port du Domain implémenté par
/// <c>Infrastructure.Repositories.Payroll.ReferentielReadRepository</c>.
/// </summary>
public interface IReferentielReadRepository
{
    Task<Result<IReadOnlyList<ReferentielItem>>> ListerGradesAsync(CancellationToken ct = default);

    Task<Result<IReadOnlyList<ReferentielItem>>> ListerCategoriesAsync(CancellationToken ct = default);

    Task<Result<IReadOnlyList<ReferentielItem>>> ListerEchelonsAsync(CancellationToken ct = default);
}
