using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Common;
using PaieEducation.Domain.Workbench.Constants;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Infrastructure.Persistence;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Infrastructure.Repositories.Workbench;

/// <summary>
/// Écrit des conditions d'éligibilité (<c>ReglesEligibilite</c>) — même
/// patron « ferme puis insère » que <c>RubriqueBaremeRepository</c>
/// (ADR-0008), sur la clé logique <c>(RubriqueId, CritereId, GroupeId)</c>.
/// </summary>
public sealed class RegleEligibiliteRepository : IRegleEligibiliteRepository
{
    private readonly SqliteConnection _connection;
    private readonly ICacheInvalidator? _cache;

    public RegleEligibiliteRepository(SqliteConnection connection, ICacheInvalidator? cache = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _cache = cache;
    }

    public async Task<Result<string>> DefinirRegleAsync(
        string rubriqueId,
        string critereId,
        string? groupeId,
        string operateur,
        string valeur,
        string dateEffet,
        string? source,
        DateTimeOffset creeLe,
        CancellationToken ct = default,
        IUnitOfWork? uow = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rubriqueId);
        ArgumentException.ThrowIfNullOrWhiteSpace(critereId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operateur);
        ArgumentException.ThrowIfNullOrWhiteSpace(valeur);
        ArgumentException.ThrowIfNullOrWhiteSpace(dateEffet);

        if (!OperateurKeys.Valides.Contains(operateur))
            return Result.Failure<string>(Error.Validation(
                $"Opérateur invalide : « {operateur} » (attendu : {string.Join(", ", OperateurKeys.Valides)})."));

        var rubriqueExiste = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("SELECT Id FROM Rubriques WHERE Id = @rubriqueId;", new { rubriqueId }, cancellationToken: ct));
        if (rubriqueExiste is null)
            return Result.Failure<string>(Error.NotFound($"Rubrique '{rubriqueId}' introuvable."));

        var critereExiste = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("SELECT Id FROM CriteresEligibilite WHERE Id = @critereId;", new { critereId }, cancellationToken: ct));
        if (critereExiste is null)
            return Result.Failure<string>(Error.NotFound($"Critère d'éligibilité '{critereId}' introuvable."));

        if (groupeId is not null)
        {
            var groupeExiste = await _connection.QuerySingleOrDefaultAsync<string>(
                new CommandDefinition("SELECT Id FROM GroupesEligibilite WHERE Id = @groupeId;", new { groupeId }, cancellationToken: ct));
            if (groupeExiste is null)
                return Result.Failure<string>(Error.NotFound($"Groupe DNF '{groupeId}' introuvable."));
        }

        var groupeCle = groupeId ?? "__COMMUNE__";

        var existeDeja = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("""
                SELECT Id FROM ReglesEligibilite
                WHERE RubriqueId = @rubriqueId AND CritereId = @critereId
                  AND ((GroupeId IS NULL AND @groupeId IS NULL) OR GroupeId = @groupeId)
                  AND DateEffet = @dateEffet;
                """,
                new { rubriqueId, critereId, groupeId, dateEffet }, cancellationToken: ct));
        if (existeDeja is not null)
            return Result.Failure<string>(Error.Conflict(
                $"Une condition est déjà définie pour '{rubriqueId}/{critereId}/{groupeCle}' à la date {dateEffet}."));

        var courante = await _connection.QuerySingleOrDefaultAsync<VersionOuverte>(
            new CommandDefinition("""
                SELECT Id, DateEffet FROM ReglesEligibilite
                WHERE RubriqueId = @rubriqueId AND CritereId = @critereId
                  AND ((GroupeId IS NULL AND @groupeId IS NULL) OR GroupeId = @groupeId)
                  AND DateFin IS NULL;
                """,
                new { rubriqueId, critereId, groupeId }, cancellationToken: ct));
        if (courante is not null && string.CompareOrdinal(dateEffet, courante.DateEffet) <= 0)
        {
            return Result.Failure<string>(Error.Validation(
                $"La nouvelle date d'effet ({dateEffet}) doit être postérieure à la version en vigueur ({courante.DateEffet})."));
        }

        var id = $"RE-{rubriqueId}-{critereId}-{groupeCle}-{dateEffet}";
        var createdAt = creeLe.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var hash = $"h-{id}";

        var uowTx = (uow as DapperUnitOfWork)?.Transaction;
        SqliteTransaction tx;
        bool ownsTx;
        if (uowTx is not null)
        {
            tx = uowTx;
            ownsTx = false;
        }
        else
        {
            tx = _connection.BeginTransaction();
            ownsTx = true;
        }

        try
        {
            if (courante is not null)
                await FermerVersionAsync(courante.Id, dateEffet, tx, ct);

            await _connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO ReglesEligibilite
                    (Id, RubriqueId, CritereId, GroupeId, Operateur, Valeur, DateEffet, DateFin, Source, Hash, CreatedAt)
                VALUES
                    (@id, @rubriqueId, @critereId, @groupeId, @operateur, @valeur, @dateEffet, NULL, @source, @hash, @createdAt);
                """,
                new
                {
                    id, rubriqueId, critereId, groupeId = (object?)groupeId ?? DBNull.Value, operateur, valeur,
                    dateEffet, source = (object?)source ?? DBNull.Value, hash, createdAt
                }, tx, cancellationToken: ct));

            if (ownsTx)
                tx.Commit();
            _cache?.Invalider();
            return Result.Success(id);
        }
        catch
        {
            if (ownsTx)
                tx?.Rollback();
            throw;
        }
    }

    public async Task<Result<string>> CloreRegleAsync(
        string regleId,
        string dateFin,
        CancellationToken ct = default,
        IUnitOfWork? uow = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(dateFin);

        var courante = await _connection.QuerySingleOrDefaultAsync<RegleRow>(
            new CommandDefinition("SELECT Id, DateEffet, DateFin FROM ReglesEligibilite WHERE Id = @regleId;",
                new { regleId }, cancellationToken: ct));
        if (courante is null)
            return Result.Failure<string>(Error.NotFound($"Condition d'éligibilité '{regleId}' introuvable."));
        if (courante.DateFin is not null)
            return Result.Failure<string>(Error.Conflict($"La condition '{regleId}' est déjà close depuis {courante.DateFin}."));
        if (string.CompareOrdinal(dateFin, courante.DateEffet) < 0)
            return Result.Failure<string>(Error.Validation(
                $"La date de fin ({dateFin}) doit être ≥ à la date d'effet ({courante.DateEffet})."));

        await _connection.ExecuteAsync(new CommandDefinition(
            "UPDATE ReglesEligibilite SET DateFin = @dateFin WHERE Id = @regleId;",
            new { dateFin, regleId }, (uow as DapperUnitOfWork)?.Transaction, cancellationToken: ct));

        _cache?.Invalider();
        return Result.Success(regleId);
    }

    private async Task FermerVersionAsync(string id, string nouvelleDateEffet, SqliteTransaction tx, CancellationToken ct)
    {
        var dateFin = DateOnly.ParseExact(nouvelleDateEffet, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            .AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        await _connection.ExecuteAsync(new CommandDefinition(
            "UPDATE ReglesEligibilite SET DateFin = @dateFin WHERE Id = @id;",
            new { dateFin, id }, tx, cancellationToken: ct));
    }

    private sealed record VersionOuverte(string Id, string DateEffet);

    private sealed record RegleRow(string Id, string DateEffet, string? DateFin);
}
