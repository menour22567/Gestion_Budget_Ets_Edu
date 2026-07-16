using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Common;
using PaieEducation.Domain.Workbench.Repositories;

namespace PaieEducation.Infrastructure.Repositories.Workbench;

/// <summary>
/// Crée des affectations suggérées (<c>AgentRubriques</c>, V011, J3H §7).
/// Lecture-écriture, Dapper.
/// </summary>
public sealed class AgentRubriqueRepository : IAgentRubriqueRepository
{
    private readonly SqliteConnection _connection;

    public AgentRubriqueRepository(SqliteConnection connection)
        => _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public async Task<Result<string?>> SuggererAsync(
        string agentId, string rubriqueId, int occurrence, string origine, string dateEffet,
        DateTimeOffset creeLe, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rubriqueId);
        ArgumentException.ThrowIfNullOrWhiteSpace(origine);
        ArgumentException.ThrowIfNullOrWhiteSpace(dateEffet);

        var existeDeja = await _connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition("""
            SELECT Id FROM AgentRubriques
            WHERE AgentId = @agentId AND RubriqueId = @rubriqueId AND Occurrence = @occurrence
              AND Statut != 'SUPPRIMEE'
              AND DateEffet <= @dateEffet AND (DateFin IS NULL OR DateFin >= @dateEffet);
            """,
            new { agentId, rubriqueId, occurrence, dateEffet }, cancellationToken: ct));
        if (existeDeja is not null)
            return Result.Success<string?>(null);

        var id = Guid.NewGuid().ToString();
        var createdAt = creeLe.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        await _connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO AgentRubriques (Id, AgentId, RubriqueId, Occurrence, Statut, Origine, DateEffet, CreatedAt)
            VALUES (@id, @agentId, @rubriqueId, @occurrence, 'SUGGEREE', @origine, @dateEffet, @createdAt);
            """,
            new { id, agentId, rubriqueId, occurrence, origine, dateEffet, createdAt }, cancellationToken: ct));

        return Result.Success<string?>(id);
    }
}
