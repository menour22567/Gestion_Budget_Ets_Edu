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
/// Écrit de nouvelles versions de barème (<c>RubriqueBaremes</c>) — même
/// patron « ferme puis insère » que <c>GrilleIndiciaireRepository</c>
/// (ADR-0008). Lecture-écriture, Dapper.
/// </summary>
public sealed class RubriqueBaremeRepository : IRubriqueBaremeRepository
{
    private readonly SqliteConnection _connection;
    private readonly ICacheInvalidator? _cache;

    public RubriqueBaremeRepository(SqliteConnection connection, ICacheInvalidator? cache = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _cache = cache;
    }

    public async Task<Result<string>> DefinirValeurBaremeAsync(
        string rubriqueId,
        string dimension,
        string borneInf,
        string? borneSup,
        string typeValeur,
        string valeur,
        string dateEffet,
        string? source,
        DateTimeOffset creeLe,
        CancellationToken ct = default,
        IUnitOfWork? uow = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rubriqueId);
        ArgumentException.ThrowIfNullOrWhiteSpace(dimension);
        ArgumentException.ThrowIfNullOrWhiteSpace(borneInf);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeValeur);
        ArgumentException.ThrowIfNullOrWhiteSpace(valeur);
        ArgumentException.ThrowIfNullOrWhiteSpace(dateEffet);

        if (!BaremeDimensionKeys.ValidesPourRubriqueBaremes.Contains(dimension))
            return Result.Failure<string>(Error.Validation(
                $"Dimension de barème invalide : « {dimension} » (attendu : {string.Join(", ", BaremeDimensionKeys.ValidesPourRubriqueBaremes)})."));

        if (!BaremeTypeValeurKeys.Valides.Contains(typeValeur))
            return Result.Failure<string>(Error.Validation(
                $"Type de valeur invalide : « {typeValeur} » (attendu : {string.Join(", ", BaremeTypeValeurKeys.Valides)})."));

        var rubriqueExiste = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("SELECT Id FROM Rubriques WHERE Id = @rubriqueId;", new { rubriqueId }, cancellationToken: ct));
        if (rubriqueExiste is null)
            return Result.Failure<string>(Error.NotFound($"Rubrique '{rubriqueId}' introuvable."));

        var existeDeja = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("""
                SELECT Id FROM RubriqueBaremes
                WHERE RubriqueId = @rubriqueId AND Dimension = @dimension AND BorneInf = @borneInf AND DateEffet = @dateEffet;
                """,
                new { rubriqueId, dimension, borneInf, dateEffet }, cancellationToken: ct));
        if (existeDeja is not null)
            return Result.Failure<string>(Error.Conflict(
                $"Un barème est déjà défini pour la tranche '{rubriqueId}/{dimension}/{borneInf}' à la date {dateEffet}."));

        var courante = await _connection.QuerySingleOrDefaultAsync<VersionCourante>(
            new CommandDefinition("""
                SELECT Id, DateEffet FROM RubriqueBaremes
                WHERE RubriqueId = @rubriqueId AND Dimension = @dimension AND BorneInf = @borneInf AND DateFin IS NULL;
                """,
                new { rubriqueId, dimension, borneInf }, cancellationToken: ct));
        if (courante is not null && string.CompareOrdinal(dateEffet, courante.DateEffet) <= 0)
        {
            return Result.Failure<string>(Error.Validation(
                $"La nouvelle date d'effet ({dateEffet}) doit être postérieure à la version en vigueur ({courante.DateEffet})."));
        }

        var id = $"RB-{rubriqueId}-{dimension}-{borneInf}-{dateEffet}";
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
                INSERT INTO RubriqueBaremes
                    (Id, RubriqueId, Dimension, BorneInf, BorneSup, TypeValeur, Valeur,
                     DateEffet, DateFin, Source, Hash, CreatedAt)
                VALUES
                    (@id, @rubriqueId, @dimension, @borneInf, @borneSup, @typeValeur, @valeur,
                     @dateEffet, NULL, @source, @hash, @createdAt);
                """,
                new
                {
                    id, rubriqueId, dimension, borneInf, borneSup = (object?)borneSup ?? DBNull.Value,
                    typeValeur, valeur, dateEffet, source = (object?)source ?? DBNull.Value, hash, createdAt
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

    private async Task FermerVersionAsync(string id, string nouvelleDateEffet, SqliteTransaction tx, CancellationToken ct)
    {
        var dateFin = DateOnly.ParseExact(nouvelleDateEffet, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            .AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        await _connection.ExecuteAsync(new CommandDefinition(
            "UPDATE RubriqueBaremes SET DateFin = @dateFin WHERE Id = @id;",
            new { dateFin, id }, tx, cancellationToken: ct));
    }

    private sealed record VersionCourante(string Id, string DateEffet);
}
