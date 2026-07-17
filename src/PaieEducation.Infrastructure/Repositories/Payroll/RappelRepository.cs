using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Rappels;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Common;

namespace PaieEducation.Infrastructure.Repositories.Payroll;

/// <summary>
/// Persiste les lignes de rappel générées (<c>Rappels</c>, V013). Lecture-écriture,
/// Dapper.
/// </summary>
public sealed class RappelRepository : IRappelRepository
{
    private readonly SqliteConnection _connection;

    public RappelRepository(SqliteConnection connection)
        => _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public async Task<Result<bool>> ExisteAsync(string agentId, string datePaieOrigine, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(datePaieOrigine);

        var existe = await _connection.QuerySingleAsync<long>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM Rappels WHERE AgentId = @agentId AND DatePaieOrigine = @datePaieOrigine;",
                new { agentId, datePaieOrigine }, cancellationToken: ct));

        return Result.Success(existe > 0);
    }

    public async Task<Result> EnregistrerAsync(
        string agentId,
        string datePaieOrigine,
        IReadOnlyList<LigneRappel> lignes,
        DateTimeOffset genereLe,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(datePaieOrigine);
        ArgumentNullException.ThrowIfNull(lignes);

        var genereLeIso = genereLe.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        using var tx = _connection.BeginTransaction();

        foreach (var ligne in lignes)
        {
            await _connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO Rappels (Id, AgentId, DatePaieOrigine, RubriqueId, MontantAncien, MontantNouveau, Delta, GenereLe, CreatedAt)
                VALUES (@id, @agentId, @datePaieOrigine, @rubriqueId, @montantAncien, @montantNouveau, @delta, @genereLe, @createdAt);
                """,
                new
                {
                    id = Guid.NewGuid().ToString(),
                    agentId,
                    datePaieOrigine,
                    rubriqueId = ligne.RubriqueId,
                    montantAncien = ligne.MontantAncien,
                    montantNouveau = ligne.MontantNouveau,
                    delta = ligne.Delta,
                    genereLe = genereLeIso,
                    createdAt = genereLeIso,
                }, tx, cancellationToken: ct));
        }

        tx.Commit();

        return Result.Success();
    }
}
