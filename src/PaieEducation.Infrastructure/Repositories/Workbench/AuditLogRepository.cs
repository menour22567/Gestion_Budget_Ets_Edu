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

    public Task<Result<IReadOnlyList<EntreeAuditLog>>> ListerAsync(CancellationToken ct = default)
        => ListerAsync(new FiltreAuditLog(TaillePage: FiltreAuditLog.TaillePageMax), ct);

    public async Task<Result<IReadOnlyList<EntreeAuditLog>>> ListerAsync(FiltreAuditLog filtre, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtre);

        if (filtre.Page < 1)
            return Result.Failure<IReadOnlyList<EntreeAuditLog>>(Error.Validation("La page doit être supérieure ou égale à 1."));
        if (filtre.TaillePage is < 1 or > FiltreAuditLog.TaillePageMax)
            return Result.Failure<IReadOnlyList<EntreeAuditLog>>(Error.Validation(
                $"La taille de page doit être comprise entre 1 et {FiltreAuditLog.TaillePageMax}."));

        var occurredAtDebut = filtre.DateDebut?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var occurredAtFin = filtre.DateFin?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var offset = (filtre.Page - 1) * filtre.TaillePage;

        var entrees = await _connection.QueryAsync<EntreeAuditLog>(new CommandDefinition("""
            SELECT Id, OccurredAt, Actor, Action, EntityType, EntityId, Payload, Comment
            FROM AuditLog
            WHERE (@actor IS NULL OR Actor = @actor)
              AND (@action IS NULL OR Action = @action)
              AND (@entityType IS NULL OR EntityType = @entityType)
              AND (@occurredAtDebut IS NULL OR OccurredAt >= @occurredAtDebut)
              AND (@occurredAtFin IS NULL OR OccurredAt <= @occurredAtFin)
            ORDER BY OccurredAt DESC, Id DESC
            LIMIT @taillePage OFFSET @offset;
            """,
            new
            {
                actor = filtre.Actor,
                action = filtre.Action,
                entityType = filtre.EntityType,
                occurredAtDebut,
                occurredAtFin,
                taillePage = filtre.TaillePage,
                offset,
            },
            cancellationToken: ct));

        return Result.Success<IReadOnlyList<EntreeAuditLog>>(entrees.ToList());
    }
}
