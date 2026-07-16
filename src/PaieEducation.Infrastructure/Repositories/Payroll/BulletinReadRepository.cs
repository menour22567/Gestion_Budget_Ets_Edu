using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Domain.Common;
using PaieEducation.Infrastructure.Serialization;

namespace PaieEducation.Infrastructure.Repositories.Payroll;

/// <summary>
/// Relit le snapshot d'un bulletin déjà validé (<c>Bulletins</c>, V012).
/// Lecture seule, Dapper. Désérialisation JSON via
/// <see cref="BulletinSnapshotJson.Options"/> (symétrique de
/// <see cref="BulletinRepository"/>).
/// </summary>
public sealed class BulletinReadRepository : IBulletinReadRepository
{
    private readonly SqliteConnection _connection;

    public BulletinReadRepository(SqliteConnection connection)
        => _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public async Task<Result<BulletinSnapshot>> ConsulterAsync(string agentId, string datePaie, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(datePaie);

        var snapshotJson = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                "SELECT SnapshotJson FROM Bulletins WHERE AgentId = @agentId AND DatePaie = @datePaie;",
                new { agentId, datePaie }, cancellationToken: ct));
        if (snapshotJson is null)
            return Result.Failure<BulletinSnapshot>(
                Error.NotFound($"Aucun bulletin validé pour l'agent '{agentId}' à la date {datePaie}."));

        var snapshot = JsonSerializer.Deserialize<BulletinSnapshot>(snapshotJson, BulletinSnapshotJson.Options);
        return Result.Success(snapshot!);
    }
}
