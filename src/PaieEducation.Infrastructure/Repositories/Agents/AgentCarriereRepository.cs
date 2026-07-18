using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Workbench.Services;

namespace PaieEducation.Infrastructure.Repositories.Agents;

/// <summary>
/// Résout un <see cref="AgentContext"/> depuis <c>Agents</c>/<c>Carrieres</c>/
/// <c>AgentAttributs</c> à une date de paie donnée (résolution point-in-time,
/// même schéma que <see cref="PaieEducation.Domain.Workbench.Services.BaremeResolver"/>).
/// Lecture seule, Dapper.
/// </summary>
/// <remarks>
/// Hors périmètre (VariableEngine, tranche suivante) : <c>Note</c>,
/// <c>ValeurPointIndiciaire</c>, <c>AssietteCotisable</c>, <c>AssietteImposable</c>
/// restent <c>null</c> — ces champs sont déjà nullables dans <see cref="AgentContext"/>,
/// aucun changement de contrat.
/// </remarks>
public sealed class AgentCarriereRepository : IAgentCarriereRepository
{
    private readonly SqliteConnection _connection;

    public AgentCarriereRepository(SqliteConnection connection)
        => _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public async Task<Result<AgentContext>> ResoudreAsync(
        string agentId, string datePaie, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(datePaie);

        var agent = await ChargerAgentAsync(agentId, ct);
        if (agent is null)
            return Result.Failure<AgentContext>(Error.NotFound($"Agent introuvable : '{agentId}'."));

        var carriere = await ChargerCarriereAsync(agentId, datePaie, ct);
        if (carriere is null)
            return Result.Failure<AgentContext>(
                Error.NotFound($"Aucune carrière en vigueur pour l'agent '{agentId}' à la date {datePaie}."));

        var origineStatutaire = await ChargerAttributAsync(agentId, "ORIGINE_STATUTAIRE", datePaie, ct)
            ?? "INCONNU"; // abstention (ADR-0009, Q-C1) : jamais de valeur déduite.

        // C2.3 — Notation agent (base PAPP, valeurSource(PAPP)). Lue depuis
        // AgentAttributs (clé NOTATION_AGENT) ; null si absente (PAPP non dû
        // plutôt que de fabriquer une note, ADR-0009).
        decimal? note = await ChargerNoteAsync(agentId, datePaie, ct);

        var anciennete = CalculerAncienneteAnnees(agent.DateRecrutement, datePaie);

        return Result.Success(new AgentContext(
            Filiere: carriere.FiliereId,
            Corps: carriere.CorpsId,
            Grade: carriere.GradeId,
            Categorie: checked((int)carriere.CategorieNiveau),
            Echelon: checked((int)carriere.EchelonNumero),
            AncienneteAnnees: anciennete,
            Fonction: carriere.FonctionId,
            TypeContrat: carriere.TypeContrat,
            TypeEtablissement: carriere.TypeEtablissement,
            OrigineStatutaire: origineStatutaire,
            Note: note,
            ValeurPointIndiciaire: null,
            AssietteCotisable: null,
            AssietteImposable: null));
    }

    private async Task<decimal?> ChargerNoteAsync(string agentId, string date, CancellationToken ct)
    {
        var raw = await ChargerAttributAsync(agentId, "NOTATION_AGENT", date, ct);
        if (raw is null) return null;
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var n)
            ? n : null;
    }

    private async Task<AgentRow?> ChargerAgentAsync(string agentId, CancellationToken ct)
    {
        const string sql = "SELECT Id, DateRecrutement FROM Agents WHERE Id = @agentId;";
        return await _connection.QuerySingleOrDefaultAsync<AgentRow>(
            new CommandDefinition(sql, new { agentId }, cancellationToken: ct));
    }

    private async Task<CarriereRow?> ChargerCarriereAsync(string agentId, string date, CancellationToken ct)
    {
        const string sql = """
            SELECT
                co.FiliereId AS FiliereId,
                g.CorpsId    AS CorpsId,
                c.GradeId    AS GradeId,
                cat.Niveau   AS CategorieNiveau,
                ech.Numero   AS EchelonNumero,
                c.FonctionId AS FonctionId,
                c.TypeContrat AS TypeContrat,
                etb.Type     AS TypeEtablissement
            FROM Carrieres c
            JOIN Grades g       ON g.Id = c.GradeId
            JOIN Corps co       ON co.Id = g.CorpsId
            JOIN Categories cat ON cat.Id = c.CategorieId
            JOIN Echelons ech   ON ech.Id = c.EchelonId
            LEFT JOIN Etablissements etb ON etb.Id = c.EtablissementId
            WHERE c.AgentId = @agentId
              AND c.DateEffet <= @date
              AND (c.DateFin IS NULL OR c.DateFin >= @date)
            ORDER BY c.DateEffet DESC LIMIT 1;
            """;
        return await _connection.QuerySingleOrDefaultAsync<CarriereRow>(
            new CommandDefinition(sql, new { agentId, date }, cancellationToken: ct));
    }

    private async Task<string?> ChargerAttributAsync(string agentId, string attribut, string date, CancellationToken ct)
    {
        const string sql = """
            SELECT Valeur FROM AgentAttributs
            WHERE AgentId = @agentId AND Attribut = @attribut
              AND DateEffet <= @date AND (DateFin IS NULL OR DateFin >= @date)
            ORDER BY DateEffet DESC LIMIT 1;
            """;
        return await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(sql, new { agentId, attribut, date }, cancellationToken: ct));
    }

    private static int CalculerAncienneteAnnees(string dateRecrutement, string datePaie)
    {
        var recrutement = DateOnly.ParseExact(dateRecrutement, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var paie = DateOnly.ParseExact(datePaie, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var annees = paie.Year - recrutement.Year;
        if (paie.Month < recrutement.Month || (paie.Month == recrutement.Month && paie.Day < recrutement.Day))
            annees--;
        return Math.Max(0, annees);
    }

    private sealed record AgentRow(string Id, string DateRecrutement);

    private sealed record CarriereRow(
        string FiliereId, string CorpsId, string GradeId, long CategorieNiveau, long EchelonNumero,
        string? FonctionId, string TypeContrat, string? TypeEtablissement);
}
