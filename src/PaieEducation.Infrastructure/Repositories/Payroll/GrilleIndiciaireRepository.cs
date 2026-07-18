using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Common;
using PaieEducation.Infrastructure.Persistence;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Infrastructure.Repositories.Payroll;

/// <summary>
/// Écrit de nouvelles versions de la grille indiciaire (<c>ValeurPoint</c>,
/// <c>GrilleIndiciaire</c>, <c>IndicesEchelon</c>, V003) — symétrique en
/// écriture de <see cref="VariableRepository"/>. Lecture-écriture, Dapper.
/// </summary>
public sealed class GrilleIndiciaireRepository : IGrilleIndiciaireRepository
{
    private readonly SqliteConnection _connection;
    private readonly ICacheInvalidator? _cache;

    public GrilleIndiciaireRepository(SqliteConnection connection, ICacheInvalidator? cache = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _cache = cache;
    }

    public async Task<Result<string>> DefinirValeurPointAsync(
        decimal valeur, string dateEffet, string version, string? source, DateTimeOffset creeLe,
        CancellationToken ct = default, IUnitOfWork? uow = null)
    {
        if (valeur <= 0)
            return Result.Failure<string>(Error.Validation("La valeur du point indiciaire doit être strictement positive."));

        var existeDeja = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                "SELECT Id FROM ValeurPoint WHERE DateEffet = @dateEffet;", new { dateEffet }, cancellationToken: ct));
        if (existeDeja is not null)
            return Result.Failure<string>(Error.Conflict($"Une valeur du point indiciaire est déjà définie à la date {dateEffet}."));

        var courante = await _connection.QuerySingleOrDefaultAsync<VersionCourante>(
            new CommandDefinition(
                "SELECT Id, DateEffet FROM ValeurPoint WHERE DateFin IS NULL;", cancellationToken: ct));
        var erreurContinuite = ValiderContinuite(courante, dateEffet);
        if (erreurContinuite is not null)
            return Result.Failure<string>(erreurContinuite);

        var id = $"VP-{dateEffet}";
        var createdAt = creeLe.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

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
                await FermerVersionAsync("ValeurPoint", courante.Id, dateEffet, tx, ct);

            await _connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO ValeurPoint (Id, DateEffet, DateFin, Valeur, Version, Source, Hash, CreatedAt)
                VALUES (@id, @dateEffet, NULL, @valeur, @version, @source, @hash, @createdAt);
                """,
                new { id, dateEffet, valeur, version, source, hash = $"h-{id}", createdAt }, tx, cancellationToken: ct));

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

    public async Task<Result<string>> DefinirIndiceMinAsync(
        string categorieId, int indiceMin, string dateEffet, string version, string? source, DateTimeOffset creeLe,
        CancellationToken ct = default, IUnitOfWork? uow = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categorieId);
        if (indiceMin <= 0)
            return Result.Failure<string>(Error.Validation("L'indice minimum de grille doit être strictement positif."));

        var existeDeja = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                "SELECT Id FROM GrilleIndiciaire WHERE CategorieId = @categorieId AND DateEffet = @dateEffet;",
                new { categorieId, dateEffet }, cancellationToken: ct));
        if (existeDeja is not null)
            return Result.Failure<string>(
                Error.Conflict($"Une grille indiciaire est déjà définie pour la catégorie '{categorieId}' à la date {dateEffet}."));

        var courante = await _connection.QuerySingleOrDefaultAsync<VersionCourante>(
            new CommandDefinition(
                "SELECT Id, DateEffet FROM GrilleIndiciaire WHERE CategorieId = @categorieId AND DateFin IS NULL;",
                new { categorieId }, cancellationToken: ct));
        var erreurContinuite = ValiderContinuite(courante, dateEffet);
        if (erreurContinuite is not null)
            return Result.Failure<string>(erreurContinuite);

        var id = $"GI-{categorieId}-{dateEffet}";
        var createdAt = creeLe.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

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
                await FermerVersionAsync("GrilleIndiciaire", courante.Id, dateEffet, tx, ct);

            await _connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO GrilleIndiciaire (Id, CategorieId, DateEffet, DateFin, IndiceMin, Version, Source, Hash, CreatedAt)
                VALUES (@id, @categorieId, @dateEffet, NULL, @indiceMin, @version, @source, @hash, @createdAt);
                """,
                new { id, categorieId, dateEffet, indiceMin, version, source, hash = $"h-{id}", createdAt }, tx, cancellationToken: ct));

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

    public async Task<Result<string>> DefinirIndiceEchelonAsync(
        string echelonId, int indice, string dateEffet, string version, string? source, DateTimeOffset creeLe,
        CancellationToken ct = default, IUnitOfWork? uow = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(echelonId);
        if (indice < 0)
            return Result.Failure<string>(Error.Validation("L'indice d'échelon ne peut pas être négatif."));

        var existeDeja = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                "SELECT Id FROM IndicesEchelon WHERE EchelonId = @echelonId AND DateEffet = @dateEffet;",
                new { echelonId, dateEffet }, cancellationToken: ct));
        if (existeDeja is not null)
            return Result.Failure<string>(
                Error.Conflict($"Un indice d'échelon est déjà défini pour l'échelon '{echelonId}' à la date {dateEffet}."));

        var courante = await _connection.QuerySingleOrDefaultAsync<VersionCourante>(
            new CommandDefinition(
                "SELECT Id, DateEffet FROM IndicesEchelon WHERE EchelonId = @echelonId AND DateFin IS NULL;",
                new { echelonId }, cancellationToken: ct));
        var erreurContinuite = ValiderContinuite(courante, dateEffet);
        if (erreurContinuite is not null)
            return Result.Failure<string>(erreurContinuite);

        var id = $"IE-{echelonId}-{dateEffet}";
        var createdAt = creeLe.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

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
                await FermerVersionAsync("IndicesEchelon", courante.Id, dateEffet, tx, ct);

            await _connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO IndicesEchelon (Id, EchelonId, DateEffet, DateFin, Indice, Version, Source, Hash, CreatedAt)
                VALUES (@id, @echelonId, @dateEffet, NULL, @indice, @version, @source, @hash, @createdAt);
                """,
                new { id, echelonId, dateEffet, indice, version, source, hash = $"h-{id}", createdAt }, tx, cancellationToken: ct));

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

    public async Task<Result<string>> DupliquerValeurPointAsync(
        string nouvelleDateEffet, string version, string? source, DateTimeOffset creeLe,
        CancellationToken ct = default, IUnitOfWork? uow = null)
    {
        var courante = await _connection.QuerySingleOrDefaultAsync<ValeurPointCourante>(
            new CommandDefinition(
                "SELECT Id, DateEffet, Valeur FROM ValeurPoint WHERE DateFin IS NULL;", cancellationToken: ct));
        if (courante is null)
            return Result.Failure<string>(Error.NotFound("Aucune valeur du point indiciaire en vigueur à dupliquer."));

        var existeDeja = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                "SELECT Id FROM ValeurPoint WHERE DateEffet = @nouvelleDateEffet;",
                new { nouvelleDateEffet }, cancellationToken: ct));
        if (existeDeja is not null)
            return Result.Failure<string>(Error.Conflict($"Une valeur du point indiciaire est déjà définie à la date {nouvelleDateEffet}."));

        var erreurContinuite = ValiderContinuite(new VersionCourante(courante.Id, courante.DateEffet), nouvelleDateEffet);
        if (erreurContinuite is not null)
            return Result.Failure<string>(erreurContinuite);

        var id = $"VP-{nouvelleDateEffet}";
        var createdAt = creeLe.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

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
            await FermerVersionAsync("ValeurPoint", courante.Id, nouvelleDateEffet, tx, ct);

            await _connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO ValeurPoint (Id, DateEffet, DateFin, Valeur, Version, Source, Hash, CreatedAt)
                VALUES (@id, @nouvelleDateEffet, NULL, @valeur, @version, @source, @hash, @createdAt);
                """,
                new { id, nouvelleDateEffet, valeur = (decimal)courante.Valeur, version, source, hash = $"h-{id}", createdAt },
                tx, cancellationToken: ct));

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

    private static Error? ValiderContinuite(VersionCourante? courante, string nouvelleDateEffet)
        => courante is not null && string.CompareOrdinal(nouvelleDateEffet, courante.DateEffet) <= 0
            ? Error.Validation(
                $"La nouvelle date d'effet ({nouvelleDateEffet}) doit être postérieure à la version en vigueur ({courante.DateEffet}).")
            : null;

    private async Task FermerVersionAsync(string table, string id, string nouvelleDateEffet, SqliteTransaction tx, CancellationToken ct)
    {
        var dateFin = DateOnly.ParseExact(nouvelleDateEffet, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            .AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        await _connection.ExecuteAsync(new CommandDefinition(
            $"UPDATE {table} SET DateFin = @dateFin WHERE Id = @id;",
            new { dateFin, id }, tx, cancellationToken: ct));
    }

    private sealed record VersionCourante(string Id, string DateEffet);

    private sealed record ValeurPointCourante(string Id, string DateEffet, double Valeur);
}
