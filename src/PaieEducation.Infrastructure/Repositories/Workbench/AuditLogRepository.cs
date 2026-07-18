using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Common;
using PaieEducation.Infrastructure.Persistence;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Infrastructure.Repositories.Workbench;

/// <summary>
/// Écrit et liste les lignes d'audit (<c>AuditLog</c>, V001). Lecture-écriture,
/// Dapper.
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
        CancellationToken ct = default,
        IUnitOfWork? uow = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);

        var occurredAtIso = occurredAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        var uowTx = (uow as DapperUnitOfWork)?.Transaction;
        await _connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO AuditLog (OccurredAt, Actor, Action, EntityType, EntityId, Payload, Comment)
            VALUES (@occurredAtIso, @actor, @action, @entityType, @entityId, @payload, @comment);
            """,
            new { occurredAtIso, actor, action, entityType, entityId, payload, comment },
            transaction: uowTx, cancellationToken: ct));

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<EntreeAuditLog>>> ListerAsync(CancellationToken ct = default)
    {
        var entrees = await _connection.QueryAsync<EntreeAuditLog>(new CommandDefinition("""
            SELECT Id, OccurredAt, Actor, Action, EntityType, EntityId, Payload, Comment
            FROM AuditLog
            ORDER BY OccurredAt DESC
            LIMIT 500;
            """, cancellationToken: ct));

        return Result.Success<IReadOnlyList<EntreeAuditLog>>(entrees.ToList());
    }
}
