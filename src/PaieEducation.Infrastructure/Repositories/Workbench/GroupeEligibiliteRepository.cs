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
/// Écrit des en-têtes de groupe DNF (<c>GroupesEligibilite</c>). L'Id est un
/// code métier fourni par l'appelant (jamais généré) — pas de « ferme puis
/// insère » sur clé composite ici (cf. remarques de
/// <see cref="IGroupeEligibiliteRepository"/>) : <see cref="DefinirGroupeAsync"/>
/// est une création pure, <see cref="CloreGroupeAsync"/> une clôture pure.
/// </summary>
public sealed class GroupeEligibiliteRepository : IGroupeEligibiliteRepository
{
    private readonly SqliteConnection _connection;
    private readonly ICacheInvalidator? _cache;

    public GroupeEligibiliteRepository(SqliteConnection connection, ICacheInvalidator? cache = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _cache = cache;
    }

    public async Task<Result<string>> DefinirGroupeAsync(
        string groupeId,
        string rubriqueId,
        string severite,
        string? messageId,
        int priorite,
        string dateEffet,
        string? dateFin,
        string? source,
        string createdBy,
        DateTimeOffset creeLe,
        CancellationToken ct = default,
        IUnitOfWork? uow = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rubriqueId);
        ArgumentException.ThrowIfNullOrWhiteSpace(severite);
        ArgumentException.ThrowIfNullOrWhiteSpace(dateEffet);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        if (!SeveriteKeys.Valides.Contains(severite))
            return Result.Failure<string>(Error.Validation(
                $"Sévérité invalide : « {severite} » (attendu : {string.Join(", ", SeveriteKeys.Valides)})."));

        if (priorite < 0)
            return Result.Failure<string>(Error.Validation("La priorité doit être ≥ 0."));

        if (dateFin is not null && string.CompareOrdinal(dateFin, dateEffet) < 0)
            return Result.Failure<string>(Error.Validation(
                $"La date de fin ({dateFin}) doit être ≥ à la date d'effet ({dateEffet})."));

        var rubriqueExiste = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("SELECT Id FROM Rubriques WHERE Id = @rubriqueId;", new { rubriqueId }, cancellationToken: ct));
        if (rubriqueExiste is null)
            return Result.Failure<string>(Error.NotFound($"Rubrique '{rubriqueId}' introuvable."));

        if (messageId is not null)
        {
            var messageExiste = await _connection.QuerySingleOrDefaultAsync<string>(
                new CommandDefinition("SELECT Id FROM MessagesRegles WHERE Id = @messageId;", new { messageId }, cancellationToken: ct));
            if (messageExiste is null)
                return Result.Failure<string>(Error.NotFound($"Message '{messageId}' introuvable."));
        }

        var groupeExiste = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("SELECT Id FROM GroupesEligibilite WHERE Id = @groupeId;", new { groupeId }, cancellationToken: ct));
        if (groupeExiste is not null)
            return Result.Failure<string>(Error.Conflict($"Un groupe DNF '{groupeId}' existe déjà."));

        var createdAt = creeLe.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var hash = $"h-{groupeId}";

        await _connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO GroupesEligibilite
                (Id, RubriqueId, Severite, MessageId, Priorite, DateEffet, DateFin, Source, Hash, CreatedAt, CreatedBy)
            VALUES
                (@groupeId, @rubriqueId, @severite, @messageId, @priorite, @dateEffet, @dateFin, @source, @hash, @createdAt, @createdBy);
            """,
            new
            {
                groupeId, rubriqueId, severite, messageId = (object?)messageId ?? DBNull.Value, priorite,
                dateEffet, dateFin = (object?)dateFin ?? DBNull.Value, source = (object?)source ?? DBNull.Value,
                hash, createdAt, createdBy
            }, (uow as DapperUnitOfWork)?.Transaction, cancellationToken: ct));

        _cache?.Invalider();
        return Result.Success(groupeId);
    }

    public async Task<Result<string>> CloreGroupeAsync(
        string groupeId,
        string dateFin,
        CancellationToken ct = default,
        IUnitOfWork? uow = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(dateFin);

        var courant = await _connection.QuerySingleOrDefaultAsync<GroupeCourant>(
            new CommandDefinition("SELECT Id, DateEffet, DateFin FROM GroupesEligibilite WHERE Id = @groupeId;",
                new { groupeId }, cancellationToken: ct));
        if (courant is null)
            return Result.Failure<string>(Error.NotFound($"Groupe DNF '{groupeId}' introuvable."));
        if (courant.DateFin is not null)
            return Result.Failure<string>(Error.Conflict($"Le groupe DNF '{groupeId}' est déjà clos depuis {courant.DateFin}."));
        if (string.CompareOrdinal(dateFin, courant.DateEffet) < 0)
            return Result.Failure<string>(Error.Validation(
                $"La date de fin ({dateFin}) doit être ≥ à la date d'effet ({courant.DateEffet})."));

        await _connection.ExecuteAsync(new CommandDefinition(
            "UPDATE GroupesEligibilite SET DateFin = @dateFin WHERE Id = @groupeId;",
            new { dateFin, groupeId }, (uow as DapperUnitOfWork)?.Transaction, cancellationToken: ct));

        _cache?.Invalider();
        return Result.Success(groupeId);
    }

    private sealed record GroupeCourant(string Id, string DateEffet, string? DateFin);
}
