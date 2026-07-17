using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Common;
using PaieEducation.Domain.Workbench.Repositories;

namespace PaieEducation.Infrastructure.Repositories.Workbench;

/// <summary>
/// Écrit une ligne d'audit (<c>AuditLog</c>, V001). Lecture-écriture, Dapper.
/// </summary>
public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly SqliteConnection _connection;

    public AuditLogRepository(SqliteConnection connection)
        => _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public async Task<Result> EnregistrerAsync(
        string actor,
        string action,
        string entityType,
        string? entityId,
        string? payload,
        string? comment,
        DateTimeOffset occurredAt,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);

        var occurredAtIso = occurredAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        await _connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO AuditLog (OccurredAt, Actor, Action, EntityType, EntityId, Payload, Comment)
            VALUES (@occurredAtIso, @actor, @action, @entityType, @entityId, @payload, @comment);
            """,
            new { occurredAtIso, actor, action, entityType, entityId, payload, comment }, cancellationToken: ct));

        return Result.Success();
    }
}
