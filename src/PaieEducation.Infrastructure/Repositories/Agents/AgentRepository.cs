using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Agents;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Infrastructure.Repositories.Agents;

/// <summary>
/// Crée un agent et sa carrière initiale (<c>Agents</c> + <c>Carrieres</c>,
/// V011) en une seule transaction. Lecture-écriture, Dapper.
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
}
