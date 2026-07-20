using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Rappels;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Shared.Money;

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

    public async Task<Result<IReadOnlyList<LigneRappel>>> ListerAsync(
        string agentId, string datePaieOrigine, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(datePaieOrigine);

        var lignes = await _connection.QueryAsync<RappelRow>(
            new CommandDefinition("""
                SELECT RubriqueId, MontantAncien, MontantNouveau, Delta
                FROM Rappels
                WHERE AgentId = @agentId AND DatePaieOrigine = @datePaieOrigine
                ORDER BY RubriqueId;
                """,
                new { agentId, datePaieOrigine }, cancellationToken: ct));

        var resultat = lignes
            .Select(r => new LigneRappel(
                r.RubriqueId, new Money((decimal)r.MontantAncien), new Money((decimal)r.MontantNouveau), new Money((decimal)r.Delta)))
            .ToList();
        return Result.Success<IReadOnlyList<LigneRappel>>(resultat);
    }

    // SQLite REAL -> double via Microsoft.Data.Sqlite ; Dapper exige une
    // correspondance exacte de type pour le mapping par constructeur d'un
    // record (piège Dapper decimal/double documenté, cf. GrilleIndiciaireRepository).
    private sealed record RappelRow(string RubriqueId, double MontantAncien, double MontantNouveau, double Delta);

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
                    montantAncien = ligne.MontantAncien.Amount,
                    montantNouveau = ligne.MontantNouveau.Amount,
                    delta = ligne.Delta.Amount,
                    genereLe = genereLeIso,
                    createdAt = genereLeIso,
                }, tx, cancellationToken: ct));
        }

        tx.Commit();

        return Result.Success();
    }
}
