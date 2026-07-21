using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Agents;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Infrastructure.Repositories.Agents;

/// <summary>
/// Écriture agent : création (<c>Agents</c> + <c>Carrieres</c> initiale, une
/// transaction), modification d'identité, nouvel événement de carrière et
/// attribut versionné (<c>AgentAttributs</c>) — V011. Lecture-écriture, Dapper.
/// </summary>
public sealed class AgentRepository : IAgentRepository
{
    private readonly SqliteConnection _connection;

    public AgentRepository(SqliteConnection connection)
        => _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public async Task<Result<string>> CreerAsync(NouvelAgent demande, DateTimeOffset creeLe, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);

        var matriculeExistant = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                "SELECT Matricule FROM Agents WHERE Matricule = @matricule;",
                new { matricule = demande.Matricule }, cancellationToken: ct));
        if (matriculeExistant is not null)
            return Result.Failure<string>(Error.Conflict($"Le matricule '{demande.Matricule}' est déjà utilisé."));

        var createdAt = creeLe.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var agentId = Guid.NewGuid().ToString();
        var carriereId = Guid.NewGuid().ToString();

        using var tx = _connection.BeginTransaction();

        await _connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, SituationFamiliale, CreatedAt)
            VALUES (@agentId, @matricule, @nom, @prenom, @dateNaissance, @dateRecrutement, @sexe, @situationFamiliale, @createdAt);
            """,
            new
            {
                agentId,
                matricule = demande.Matricule,
                nom = demande.Nom,
                prenom = demande.Prenom,
                dateNaissance = demande.DateNaissance,
                dateRecrutement = demande.DateRecrutement,
                sexe = demande.Sexe,
                situationFamiliale = demande.SituationFamiliale,
                createdAt,
            }, tx, cancellationToken: ct));

        await _connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO Carrieres (Id, AgentId, GradeId, CategorieId, EchelonId, FonctionId, TypeContrat, EtablissementId, DateEffet, Motif, NumeroDecision, CreatedAt)
            VALUES (@carriereId, @agentId, @gradeId, @categorieId, @echelonId, @fonctionId, @typeContrat, @etablissementId, @dateEffet, 'Recrutement', @numeroDecision, @createdAt);
            """,
            new
            {
                carriereId,
                agentId,
                gradeId = demande.GradeId,
                categorieId = demande.CategorieId,
                echelonId = demande.EchelonId,
                fonctionId = demande.FonctionId,
                typeContrat = demande.TypeContrat,
                etablissementId = demande.EtablissementId,
                dateEffet = demande.DateRecrutement,
                numeroDecision = demande.NumeroDecision,
                createdAt,
            }, tx, cancellationToken: ct));

        tx.Commit();
        return Result.Success(agentId);
    }

    public async Task<Result<string>> ModifierAsync(AgentModifie demande, DateTimeOffset modifieLe, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);

        var existant = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("SELECT Id FROM Agents WHERE Id = @id;", new { id = demande.AgentId }, cancellationToken: ct));
        if (existant is null)
            return Result.Failure<string>(Error.NotFound($"Agent '{demande.AgentId}' introuvable."));

        var updatedAt = modifieLe.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        await _connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Agents
            SET Nom = @nom, Prenom = @prenom, DateNaissance = @dateNaissance,
                Sexe = @sexe, SituationFamiliale = @situationFamiliale, Statut = @statut,
                UpdatedAt = @updatedAt
            WHERE Id = @id;
            """,
            new
            {
                id = demande.AgentId, nom = demande.Nom, prenom = demande.Prenom,
                dateNaissance = demande.DateNaissance, sexe = demande.Sexe,
                situationFamiliale = demande.SituationFamiliale, statut = demande.Statut, updatedAt,
            }, cancellationToken: ct));

        return Result.Success(demande.AgentId);
    }

    public async Task<Result<string>> EnregistrerEvenementCarriereAsync(
        EvenementCarriere demande, DateTimeOffset creeLe, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);

        var agentExiste = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("SELECT Id FROM Agents WHERE Id = @id;", new { id = demande.AgentId }, cancellationToken: ct));
        if (agentExiste is null)
            return Result.Failure<string>(Error.NotFound($"Agent '{demande.AgentId}' introuvable."));

        var existeDeja = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                "SELECT Id FROM Carrieres WHERE AgentId = @agentId AND DateEffet = @dateEffet;",
                new { agentId = demande.AgentId, dateEffet = demande.DateEffet }, cancellationToken: ct));
        if (existeDeja is not null)
            return Result.Failure<string>(
                Error.Conflict($"Une carrière existe déjà pour l'agent '{demande.AgentId}' à la date {demande.DateEffet}."));

        var courante = await _connection.QuerySingleOrDefaultAsync<VersionCourante>(
            new CommandDefinition(
                "SELECT Id, DateEffet FROM Carrieres WHERE AgentId = @agentId AND DateFin IS NULL;",
                new { agentId = demande.AgentId }, cancellationToken: ct));
        var erreurContinuite = ValiderContinuite(courante, demande.DateEffet);
        if (erreurContinuite is not null)
            return Result.Failure<string>(erreurContinuite);

        var carriereId = Guid.NewGuid().ToString();
        var createdAt = creeLe.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        using var tx = _connection.BeginTransaction();
        if (courante is not null)
            await FermerVersionAsync("Carrieres", courante.Id, demande.DateEffet, tx, ct);

        await _connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO Carrieres (Id, AgentId, GradeId, CategorieId, EchelonId, FonctionId, TypeContrat, EtablissementId, DateEffet, Motif, NumeroDecision, Source, CreatedAt)
            VALUES (@carriereId, @agentId, @gradeId, @categorieId, @echelonId, @fonctionId, @typeContrat, @etablissementId, @dateEffet, @motif, @numeroDecision, @source, @createdAt);
            """,
            new
            {
                carriereId,
                agentId = demande.AgentId,
                gradeId = demande.GradeId,
                categorieId = demande.CategorieId,
                echelonId = demande.EchelonId,
                fonctionId = demande.FonctionId,
                typeContrat = demande.TypeContrat,
                etablissementId = demande.EtablissementId,
                dateEffet = demande.DateEffet,
                motif = demande.Motif,
                numeroDecision = demande.NumeroDecision,
                source = demande.Source,
                createdAt,
            }, tx, cancellationToken: ct));

        tx.Commit();
        return Result.Success(carriereId);
    }

    public async Task<Result<string>> DefinirAttributAsync(
        string agentId, string attribut, string valeur, string dateEffet, string? source,
        DateTimeOffset creeLe, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(attribut);
        ArgumentException.ThrowIfNullOrWhiteSpace(valeur);
        ArgumentException.ThrowIfNullOrWhiteSpace(dateEffet);

        var agentExiste = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("SELECT Id FROM Agents WHERE Id = @id;", new { id = agentId }, cancellationToken: ct));
        if (agentExiste is null)
            return Result.Failure<string>(Error.NotFound($"Agent '{agentId}' introuvable."));

        var existeDeja = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                "SELECT Id FROM AgentAttributs WHERE AgentId = @agentId AND Attribut = @attribut AND DateEffet = @dateEffet;",
                new { agentId, attribut, dateEffet }, cancellationToken: ct));
        if (existeDeja is not null)
            return Result.Failure<string>(
                Error.Conflict($"L'attribut « {attribut} » est déjà défini pour l'agent '{agentId}' à la date {dateEffet}."));

        var courant = await _connection.QuerySingleOrDefaultAsync<VersionCourante>(
            new CommandDefinition(
                "SELECT Id, DateEffet FROM AgentAttributs WHERE AgentId = @agentId AND Attribut = @attribut AND DateFin IS NULL;",
                new { agentId, attribut }, cancellationToken: ct));
        var erreurContinuite = ValiderContinuite(courant, dateEffet);
        if (erreurContinuite is not null)
            return Result.Failure<string>(erreurContinuite);

        var id = Guid.NewGuid().ToString();
        var createdAt = creeLe.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        using var tx = _connection.BeginTransaction();
        if (courant is not null)
            await FermerVersionAsync("AgentAttributs", courant.Id, dateEffet, tx, ct);

        await _connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO AgentAttributs (Id, AgentId, Attribut, Valeur, DateEffet, DateFin, Source, CreatedAt)
            VALUES (@id, @agentId, @attribut, @valeur, @dateEffet, NULL, @source, @createdAt);
            """,
            new { id, agentId, attribut, valeur, dateEffet, source, createdAt }, tx, cancellationToken: ct));

        tx.Commit();
        return Result.Success(id);
    }

    /// <summary>
    /// Continuité temporelle (même invariant que <c>RubriqueRepository</c> :
    /// <c>DefinirParametreAsync</c>/<c>DefinirValeurBareme</c>) : la nouvelle
    /// date d'effet doit être strictement postérieure à la version en vigueur.
    /// </summary>
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
}
