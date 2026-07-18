using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.ValueObjects;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Infrastructure.Repositories.Payroll;

/// <summary>
/// Liste la nomenclature (<c>Grades</c>, <c>Categories</c>, <c>Echelons</c>,
/// V002). Lecture seule, Dapper.
/// </summary>
public sealed class ReferentielReadRepository : IReferentielReadRepository
{
    private readonly SqliteConnection _connection;

    public ReferentielReadRepository(SqliteConnection connection)
        => _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public Task<Result<IReadOnlyList<ReferentielItem>>> ListerGradesAsync(CancellationToken ct = default)
        => ListerAsync("Grades", ct);

    public Task<Result<IReadOnlyList<ReferentielItem>>> ListerCategoriesAsync(CancellationToken ct = default)
        => ListerAsync("Categories", ct);

    public Task<Result<IReadOnlyList<ReferentielItem>>> ListerEchelonsAsync(CancellationToken ct = default)
        => ListerAsync("Echelons", ct);

    private async Task<Result<IReadOnlyList<ReferentielItem>>> ListerAsync(string table, CancellationToken ct)
    {
        var items = await _connection.QueryAsync<ReferentielItem>(new CommandDefinition(
            $"SELECT Id, Libelle FROM {table} WHERE Actif = 1 ORDER BY Id;", cancellationToken: ct));

        return Result.Success<IReadOnlyList<ReferentielItem>>(items.ToList());
    }
}
