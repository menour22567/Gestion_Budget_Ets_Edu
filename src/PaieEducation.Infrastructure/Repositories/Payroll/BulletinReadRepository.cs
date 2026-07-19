using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
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

    public async Task<Result<(BulletinSnapshot Snapshot, string BulletinId)>> ConsulterAvecBulletinIdAsync(
        string agentId, string datePaie, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(datePaie);

        var row = await _connection.QuerySingleOrDefaultAsync<(string Id, string SnapshotJson)?>(
            new CommandDefinition(
                "SELECT Id, SnapshotJson FROM Bulletins WHERE AgentId = @agentId AND DatePaie = @datePaie;",
                new { agentId, datePaie }, cancellationToken: ct));
        if (row is null)
            return Result.Failure<(BulletinSnapshot, string)>(
                Error.NotFound($"Aucun bulletin validé pour l'agent '{agentId}' à la date {datePaie}."));

        var snapshot = JsonSerializer.Deserialize<BulletinSnapshot>(row.Value.SnapshotJson, BulletinSnapshotJson.Options);
        return Result.Success((snapshot!, row.Value.Id));
    }

    public async Task<Result<IReadOnlyList<BulletinSnapshot>>> ListerPourPeriodeAsync(
        string agentId, string periodeDebut, string? periodeFin, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(periodeDebut);

        const string sql = """
            SELECT SnapshotJson FROM Bulletins
            WHERE AgentId = @agentId
              AND DatePaie >= @periodeDebut
              AND (@periodeFin IS NULL OR DatePaie <= @periodeFin)
            ORDER BY DatePaie ASC;
            """;
        var rows = await _connection.QueryAsync<string>(
            new CommandDefinition(sql, new { agentId, periodeDebut, periodeFin }, cancellationToken: ct));

        var snapshots = new List<BulletinSnapshot>(rows.Count());
        foreach (var json in rows)
        {
            var snap = JsonSerializer.Deserialize<BulletinSnapshot>(json, BulletinSnapshotJson.Options);
            if (snap is not null) snapshots.Add(snap);
        }
        return Result.Success<IReadOnlyList<BulletinSnapshot>>(snapshots);
    }

    public async Task<Result<int>> CompterPourPeriodeAsync(
        string periodeDebut, string? periodeFin, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(periodeDebut);

        // D8 / ADR-0007 : utilisé par le simulateur d'impact pour compter les
        // bulletins validés dans la période rétroactive touchée par une
        // évolution. periodeFin == null = période ouverte (jusqu'à aujourd'hui).
        // Pas de filtre AgentId : on veut le total période, toutes rubriques
        // confondues. Si l'appelant veut un décompte par agent, il appelle
        // ConsulterAsync(agentId, datePaie) pour chaque bulletin et compte.
        const string sql = """
            SELECT COUNT(*) FROM Bulletins
            WHERE DatePaie >= @periodeDebut
              AND (@periodeFin IS NULL OR DatePaie <= @periodeFin);
            """;
        var count = await _connection.QuerySingleAsync<long>(
            new CommandDefinition(sql, new { periodeDebut, periodeFin }, cancellationToken: ct));
        return Result.Success((int)count);
    }
}
