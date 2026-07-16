using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Common;
using PaieEducation.Domain.Workbench.Services;

namespace PaieEducation.Infrastructure.Repositories.Payroll;

/// <summary>
/// Résout les variables de base d'un bulletin (<c>INDICE_MIN</c>, <c>INDICE_ECH</c>,
/// <c>VPI</c>, <c>TBASE</c>, <c>TRT</c>, <c>ECH</c>, <c>CAT</c>) depuis
/// <c>GrilleIndiciaire</c>/<c>IndicesEchelon</c>/<c>ValeurPoint</c> à une date de paie
/// donnée (résolution point-in-time, même schéma que
/// <see cref="PaieEducation.Infrastructure.Repositories.Agents.AgentCarriereRepository"/>).
/// Lecture seule, Dapper.
/// </summary>
/// <remarks>
/// <c>Categories.Niveau</c> et <c>Echelons.Numero</c> sont uniques (V002) : on résout
/// donc <c>GrilleIndiciaire</c>/<c>IndicesEchelon</c> (qui référencent <c>CategorieId</c>/
/// <c>EchelonId</c> texte) directement depuis les entiers <c>AgentContext.Categorie</c>/
/// <c>Echelon</c>, sans avoir besoin des ID texte sur le contexte agent.
/// Hors périmètre : <c>ClesBareme</c> et <c>SourcesValeur</c> (mécanismes séparés,
/// barème et <c>ISourceValeurResolver</c>).
/// </remarks>
public sealed class VariableRepository : IVariableRepository
{
    private readonly SqliteConnection _connection;

    public VariableRepository(SqliteConnection connection)
        => _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public async Task<Result<IReadOnlyDictionary<string, decimal>>> ResoudreAsync(
        AgentContext agent, string datePaie, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentException.ThrowIfNullOrWhiteSpace(datePaie);

        if (agent.Categorie is null || agent.Echelon is null)
            return Result.Failure<IReadOnlyDictionary<string, decimal>>(
                Error.Validation("Categorie et Echelon sont requis sur l'AgentContext pour résoudre les variables de base."));

        var categorie = agent.Categorie.Value;
        var echelon = agent.Echelon.Value;

        var indiceMin = await ChargerIndiceMinAsync(categorie, datePaie, ct);
        if (indiceMin is null)
            return Result.Failure<IReadOnlyDictionary<string, decimal>>(
                Error.NotFound($"Aucune grille indiciaire en vigueur pour la catégorie {categorie} à la date {datePaie}."));

        var indiceEch = await ChargerIndiceEchelonAsync(echelon, datePaie, ct);
        if (indiceEch is null)
            return Result.Failure<IReadOnlyDictionary<string, decimal>>(
                Error.NotFound($"Aucun indice d'échelon en vigueur pour l'échelon {echelon} à la date {datePaie}."));

        var vpi = await ChargerValeurPointAsync(datePaie, ct);
        if (vpi is null)
            return Result.Failure<IReadOnlyDictionary<string, decimal>>(
                Error.NotFound($"Aucune valeur du point indiciaire en vigueur à la date {datePaie}."));

        var tbase = indiceMin.Value * vpi.Value;
        var trt = (indiceMin.Value + indiceEch.Value) * vpi.Value;

        return Result.Success<IReadOnlyDictionary<string, decimal>>(new Dictionary<string, decimal>
        {
            ["INDICE_MIN"] = indiceMin.Value,
            ["INDICE_ECH"] = indiceEch.Value,
            ["VPI"] = vpi.Value,
            ["TBASE"] = tbase,
            ["TRT"] = trt,
            ["ECH"] = echelon,
            ["CAT"] = categorie,
        });
    }

    private async Task<decimal?> ChargerIndiceMinAsync(int categorieNiveau, string date, CancellationToken ct)
    {
        const string sql = """
            SELECT gi.IndiceMin
            FROM GrilleIndiciaire gi
            JOIN Categories cat ON cat.Id = gi.CategorieId
            WHERE cat.Niveau = @categorieNiveau
              AND gi.DateEffet <= @date AND (gi.DateFin IS NULL OR gi.DateFin >= @date)
            ORDER BY gi.DateEffet DESC LIMIT 1;
            """;
        var indice = await _connection.QuerySingleOrDefaultAsync<long?>(
            new CommandDefinition(sql, new { categorieNiveau, date }, cancellationToken: ct));
        return indice is null ? null : (decimal)indice.Value;
    }

    private async Task<decimal?> ChargerIndiceEchelonAsync(int echelonNumero, string date, CancellationToken ct)
    {
        const string sql = """
            SELECT ie.Indice
            FROM IndicesEchelon ie
            JOIN Echelons ech ON ech.Id = ie.EchelonId
            WHERE ech.Numero = @echelonNumero
              AND ie.DateEffet <= @date AND (ie.DateFin IS NULL OR ie.DateFin >= @date)
            ORDER BY ie.DateEffet DESC LIMIT 1;
            """;
        var indice = await _connection.QuerySingleOrDefaultAsync<long?>(
            new CommandDefinition(sql, new { echelonNumero, date }, cancellationToken: ct));
        return indice is null ? null : (decimal)indice.Value;
    }

    private async Task<decimal?> ChargerValeurPointAsync(string date, CancellationToken ct)
    {
        const string sql = """
            SELECT Valeur FROM ValeurPoint
            WHERE DateEffet <= @date AND (DateFin IS NULL OR DateFin >= @date)
            ORDER BY DateEffet DESC LIMIT 1;
            """;
        var valeur = await _connection.QuerySingleOrDefaultAsync<double?>(
            new CommandDefinition(sql, new { date }, cancellationToken: ct));
        return valeur is null ? null : (decimal)valeur.Value;
    }
}
