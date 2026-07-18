using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Common;
using PaieEducation.Domain.Workbench.Constants;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Infrastructure.Repositories.Workbench;

/// <summary>
/// Crée des affectations suggérées (<c>AgentRubriques</c>, V011, J3H §7).
/// Lecture-écriture, Dapper.
/// </summary>
public sealed class AgentRubriqueRepository : IAgentRubriqueRepository
{
    private readonly SqliteConnection _connection;
    private readonly ICacheInvalidator? _cache;

    public AgentRubriqueRepository(SqliteConnection connection, ICacheInvalidator? cache = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _cache = cache;
    }

    public async Task<Result<string?>> SuggererAsync(
        string agentId, string rubriqueId, int occurrence, string origine, string dateEffet,
        DateTimeOffset creeLe, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rubriqueId);
        ArgumentException.ThrowIfNullOrWhiteSpace(origine);
        ArgumentException.ThrowIfNullOrWhiteSpace(dateEffet);

        var existeDeja = await _connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition($"""
            SELECT Id FROM AgentRubriques
            WHERE AgentId = @agentId AND RubriqueId = @rubriqueId AND Occurrence = @occurrence
              AND Statut != '{StatutAffectation.Supprimee}'
              AND DateEffet <= @dateEffet AND (DateFin IS NULL OR DateFin >= @dateEffet);
            """,
            new { agentId, rubriqueId, occurrence, dateEffet }, cancellationToken: ct));
        if (existeDeja is not null)
            return Result.Success<string?>(null);

        var id = Guid.NewGuid().ToString();
        var createdAt = creeLe.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        await _connection.ExecuteAsync(new CommandDefinition($"""
            INSERT INTO AgentRubriques (Id, AgentId, RubriqueId, Occurrence, Statut, Origine, DateEffet, CreatedAt)
            VALUES (@id, @agentId, @rubriqueId, @occurrence, '{StatutAffectation.Suggerer}', @origine, @dateEffet, @createdAt);
            """,
            new { id, agentId, rubriqueId, occurrence, origine, dateEffet, createdAt }, cancellationToken: ct));

        _cache?.Invalider();
        return Result.Success<string?>(id);
    }

    public async Task<Result<IReadOnlyList<AffectationRubrique>>> ListerParAgentAsync(
        string agentId, string datePaie, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(datePaie);

        var rows = await _connection.QueryAsync<AffectationRow>(new CommandDefinition("""
            SELECT Id, RubriqueId, Occurrence, Statut, Origine, DateEffet, DateFin
            FROM AgentRubriques
            WHERE AgentId = @agentId
              AND DateEffet <= @datePaie AND (DateFin IS NULL OR DateFin >= @datePaie)
            ORDER BY DateEffet DESC, RubriqueId;
            """,
            new { agentId, datePaie }, cancellationToken: ct));

        var affectations = rows
            .Select(r => new AffectationRubrique(
                r.Id, r.RubriqueId, checked((int)r.Occurrence), r.Statut, r.Origine, r.DateEffet, r.DateFin))
            .ToList();
        return Result.Success<IReadOnlyList<AffectationRubrique>>(affectations);
    }

    public async Task<Result<string>> ChangerStatutAsync(
        string agentRubriqueId, string nouveauStatut, DateTimeOffset maintenant, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentRubriqueId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nouveauStatut);

        var statutActuel = await _connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT Statut FROM AgentRubriques WHERE Id = @agentRubriqueId;",
            new { agentRubriqueId }, cancellationToken: ct));
        if (statutActuel is null)
            return Result.Failure<string>(Error.NotFound($"Affectation introuvable : '{agentRubriqueId}'."));
        if (statutActuel == StatutAffectation.Supprimee)
            return Result.Failure<string>(Error.Conflict(
                $"L'affectation '{agentRubriqueId}' est à l'état terminal {StatutAffectation.Supprimee} — créer une nouvelle ligne plutôt que la modifier."));

        var updatedAt = maintenant.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        await _connection.ExecuteAsync(new CommandDefinition(
            "UPDATE AgentRubriques SET Statut = @nouveauStatut, UpdatedAt = @updatedAt WHERE Id = @agentRubriqueId;",
            new { nouveauStatut, updatedAt, agentRubriqueId }, cancellationToken: ct));

        _cache?.Invalider();
        return Result.Success(agentRubriqueId);
    }

    private sealed record AffectationRow(
        string Id, string RubriqueId, long Occurrence, string Statut, string Origine, string DateEffet, string? DateFin);
}
