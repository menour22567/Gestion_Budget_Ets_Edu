using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Infrastructure.Repositories.Agents;

/// <summary>
/// Liste les agents existants (<c>Agents</c>, V011) pour un sélecteur de l'UI.
/// Lecture seule, Dapper. Connexion SQLite ouverte et partagée par le
/// Composition Root (scoped).
/// </summary>
public sealed class AgentReadRepository : IAgentReadRepository
{
    private readonly SqliteConnection _connection;

    public AgentReadRepository(SqliteConnection connection)
        => _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public async Task<Result<IReadOnlyList<AgentResume>>> ListerAsync(CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(_connection);

        var lignes = await _connection.QueryAsync<AgentRow>(new CommandDefinition(
            "SELECT Id, Matricule, Nom, Prenom FROM Agents ORDER BY Matricule ASC;",
            cancellationToken: ct));

        var resumes = lignes
            .Select(r => new AgentResume(r.Id, r.Matricule, r.Nom, r.Prenom))
            .ToList();
        return Result.Success<IReadOnlyList<AgentResume>>(resumes);
    }

    public Task<Result<IReadOnlyList<NomenclatureItem>>> ListerSexesAsync(CancellationToken ct = default)
        => ListerNomenclatureAsync("TypesSexe", ct);

    public Task<Result<IReadOnlyList<NomenclatureItem>>> ListerSituationsFamilialesAsync(CancellationToken ct = default)
        => ListerNomenclatureAsync("SituationsFamiliales", ct);

    public Task<Result<IReadOnlyList<NomenclatureItem>>> ListerTypesContratAsync(CancellationToken ct = default)
        => ListerNomenclatureAsync("TypesContrat", ct);

    private async Task<Result<IReadOnlyList<NomenclatureItem>>> ListerNomenclatureAsync(string table, CancellationToken ct)
    {
        var items = await _connection.QueryAsync<NomenclatureItem>(new CommandDefinition(
            $"SELECT Id, Libelle FROM {table} WHERE Actif = 1 ORDER BY Id;",
            cancellationToken: ct));
        return Result.Success<IReadOnlyList<NomenclatureItem>>(items.ToList());
    }

    private sealed class AgentRow
    {
        public string Id { get; set; } = string.Empty;
        public string Matricule { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
    }
}
