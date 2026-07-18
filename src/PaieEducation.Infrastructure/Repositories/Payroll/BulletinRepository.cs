using System.Globalization;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Infrastructure.Serialization;

namespace PaieEducation.Infrastructure.Repositories.Payroll;

/// <summary>
/// Persiste le snapshot d'un bulletin validé (<c>Bulletins</c>, V012).
/// Lecture-écriture, Dapper. Sérialisation JSON via
/// <see cref="BulletinSnapshotJson.Options"/>.
/// </summary>
public sealed class BulletinRepository : IBulletinRepository
{
    private readonly SqliteConnection _connection;

    public BulletinRepository(SqliteConnection connection)
        => _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public async Task<Result<string>> ValiderAsync(
        string agentId, BulletinSnapshot snapshot, DateTimeOffset valideLe, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentNullException.ThrowIfNull(snapshot);

        var datePaie = snapshot.Input.DatePaie;

        var dejaValide = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                "SELECT Id FROM Bulletins WHERE AgentId = @agentId AND DatePaie = @datePaie;",
                new { agentId, datePaie }, cancellationToken: ct));
        if (dejaValide is not null)
            return Result.Failure<string>(
                Error.Conflict($"Un bulletin est déjà validé pour l'agent '{agentId}' à la date {datePaie}."));

        var bulletinId = Guid.NewGuid().ToString();
        var snapshotJson = JsonSerializer.Serialize(snapshot, BulletinSnapshotJson.Options);
        var createdAt = valideLe.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        await _connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO Bulletins (Id, AgentId, DatePaie, Net, TotalGains, AssietteImposable, Irg, SnapshotJson, ValideLe, CreatedAt)
            VALUES (@bulletinId, @agentId, @datePaie, @net, @totalGains, @assietteImposable, @irg, @snapshotJson, @valideLe, @createdAt);
            """,
            new
            {
                bulletinId,
                agentId,
                datePaie,
                net = snapshot.Resultat.Net.Amount,
                totalGains = snapshot.Resultat.TotalGains.Amount,
                assietteImposable = snapshot.Resultat.AssietteImposable.Amount,
                irg = snapshot.Resultat.Irg.Amount,
                snapshotJson,
                valideLe = createdAt,
                createdAt,
            }, cancellationToken: ct));

        return Result.Success(bulletinId);
    }
}
