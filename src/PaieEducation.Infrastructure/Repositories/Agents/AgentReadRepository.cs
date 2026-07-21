using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Infrastructure.Repositories.Agents;

/// <summary>
/// Liste les agents existants (<c>Agents</c>, V011) pour un sélecteur de l'UI.
/// Lecture seule, Dapper. Connexion SQLite ouverte et partagée par le
/// Composition Root (scoped).
/// </summary>
public sealed class AgentReadRepository : IAgentReadRepository
{
    private readonly SqliteConnection _connection;

    public AgentReadRepository(SqliteConnection connection)
        => _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public async Task<Result<IReadOnlyList<AgentResume>>> ListerAsync(CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(_connection);

        var lignes = await _connection.QueryAsync<AgentRow>(new CommandDefinition(
            "SELECT Id, Matricule, Nom, Prenom FROM Agents ORDER BY Matricule ASC;",
            cancellationToken: ct));

        var resumes = lignes
            .Select(r => new AgentResume(r.Id, r.Matricule, r.Nom, r.Prenom))
            .ToList();
        return Result.Success<IReadOnlyList<AgentResume>>(resumes);
    }

    public async Task<AgentDetail?> ObtenirAsync(string agentId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        // Identité (Agents) + carrière la plus récente (dernière DateEffet) avec
        // ses libellés. LEFT JOIN sur la carrière : un agent sans carrière reste
        // renvoyé (identité seule). Fonction/Établissement sont nullables.
        const string sql = """
            SELECT
                a.Id, a.Matricule, a.Nom, a.Prenom, a.DateNaissance, a.DateRecrutement,
                a.Sexe, a.SituationFamiliale, a.Statut,
                c.GradeId       AS GradeId,
                g.Libelle       AS GradeLibelle,
                co.Libelle      AS CorpsLibelle,
                cat.Niveau      AS CategorieNiveau,
                ech.Numero      AS EchelonNumero,
                c.TypeContrat   AS TypeContrat,
                f.Libelle       AS FonctionLibelle,
                etb.Nom         AS EtablissementNom,
                etb.Type        AS EtablissementType,
                c.DateEffet     AS CarriereDepuis,
                c.Motif         AS CarriereMotif,
                c.FonctionId       AS FonctionId,
                c.EtablissementId  AS EtablissementId
            FROM Agents a
            LEFT JOIN (
                SELECT * FROM Carrieres WHERE AgentId = $agentId ORDER BY DateEffet DESC LIMIT 1
            ) c            ON c.AgentId = a.Id
            LEFT JOIN Grades g          ON g.Id = c.GradeId
            LEFT JOIN Corps co          ON co.Id = g.CorpsId
            LEFT JOIN Categories cat    ON cat.Id = c.CategorieId
            LEFT JOIN Echelons ech      ON ech.Id = c.EchelonId
            LEFT JOIN Fonctions f       ON f.Id = c.FonctionId
            LEFT JOIN Etablissements etb ON etb.Id = c.EtablissementId
            WHERE a.Id = $agentId;
            """;

        var row = await _connection.QuerySingleOrDefaultAsync<DetailRow>(
            new CommandDefinition(sql, new { agentId }, cancellationToken: ct));
        if (row is null) return null;

        return new AgentDetail(
            row.Id, row.Matricule, row.Nom, row.Prenom, row.DateNaissance, row.DateRecrutement,
            row.Sexe, row.SituationFamiliale, row.Statut,
            row.GradeId, row.GradeLibelle, row.CorpsLibelle,
            (int?)row.CategorieNiveau, (int?)row.EchelonNumero,
            row.TypeContrat, row.FonctionLibelle, row.EtablissementNom, row.EtablissementType,
            row.CarriereDepuis, row.CarriereMotif, row.FonctionId, row.EtablissementId);
    }

    public Task<Result<IReadOnlyList<NomenclatureItem>>> ListerSexesAsync(CancellationToken ct = default)
        => ListerNomenclatureAsync("TypesSexe", ct);

    public Task<Result<IReadOnlyList<NomenclatureItem>>> ListerSituationsFamilialesAsync(CancellationToken ct = default)
        => ListerNomenclatureAsync("SituationsFamiliales", ct);

    public Task<Result<IReadOnlyList<NomenclatureItem>>> ListerTypesContratAsync(CancellationToken ct = default)
        => ListerNomenclatureAsync("TypesContrat", ct);

    private async Task<Result<IReadOnlyList<NomenclatureItem>>> ListerNomenclatureAsync(string table, CancellationToken ct)
    {
        var items = await _connection.QueryAsync<NomenclatureItem>(new CommandDefinition(
            $"SELECT Id, Libelle FROM {table} WHERE Actif = 1 ORDER BY Id;",
            cancellationToken: ct));
        return Result.Success<IReadOnlyList<NomenclatureItem>>(items.ToList());
    }

    private sealed class AgentRow
    {
        public string Id { get; set; } = string.Empty;
        public string Matricule { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
    }

    // Numériques de grille lus en long? (SQLite INTEGER) puis convertis en int?
    // — même précaution que AgentCarriereRepository (mapping par propriété, pas
    // par constructeur, pour éviter les pièges de conversion Dapper).
    private sealed class DetailRow
    {
        public string Id { get; set; } = string.Empty;
        public string Matricule { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
        public string DateNaissance { get; set; } = string.Empty;
        public string DateRecrutement { get; set; } = string.Empty;
        public string Sexe { get; set; } = string.Empty;
        public string SituationFamiliale { get; set; } = string.Empty;
        public string Statut { get; set; } = string.Empty;
        public string? GradeId { get; set; }
        public string? GradeLibelle { get; set; }
        public string? CorpsLibelle { get; set; }
        public long? CategorieNiveau { get; set; }
        public long? EchelonNumero { get; set; }
        public string? TypeContrat { get; set; }
        public string? FonctionLibelle { get; set; }
        public string? EtablissementNom { get; set; }
        public string? EtablissementType { get; set; }
        public string? CarriereDepuis { get; set; }
        public string? CarriereMotif { get; set; }
        public string? FonctionId { get; set; }
        public string? EtablissementId { get; set; }
    }
}
