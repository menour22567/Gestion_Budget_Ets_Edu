using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Infrastructure.Repositories.Payroll;

/// <summary>
/// Implémentation SQLite de <see cref="IParametreSystemeRepository"/>.
/// Lit les paramètres système versionnés (table <c>Parametres</c>) avec
/// résolution point-in-time.
/// </summary>
public sealed class ParametreSystemeRepository : IParametreSystemeRepository
{
    private readonly SqliteConnection _connection;

    public ParametreSystemeRepository(SqliteConnection connection)
        => _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public async Task<Result<string?>> LireValeurAsync(string cle, string dateEffet, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cle);

        const string sql = """
            SELECT Valeur FROM Parametres
            WHERE Cle = @cle
              AND DateEffet <= @date
            ORDER BY DateEffet DESC LIMIT 1;
            """;
        var valeur = await _connection.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(sql, new { cle, date = dateEffet }, cancellationToken: ct));
        return Result.Success<string?>(valeur);
    }

    public async Task<Result<ModeArrondi>> LireModeArrondiAsync(string dateEffet, CancellationToken ct = default)
    {
        var valeur = await LireValeurAsync("ARRONDI_MODE", dateEffet, ct);
        if (valeur.IsFailure)
            return Result.Failure<ModeArrondi>(valeur.Error);

        // Défaut seedé (Q9b) : dinar le plus proche si absent.
        if (valeur.Value is null)
            return Result.Success(ModeArrondi.DinarPlusProche);

        var mode = ArrondiService.ParserMode(valeur.Value);
        // En cas de valeur corrompue, on retombe sur le défaut plutôt que d'échouer
        // tout le calcul (robustesse d'exploitation).
        return mode.IsFailure ? Result.Success(ModeArrondi.DinarPlusProche) : Result.Success(mode.Value);
    }

    public async Task<Result<decimal>> LireDecimalAsync(string cle, decimal defaut, string dateEffet, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cle);

        var valeur = await LireValeurAsync(cle, dateEffet, ct);
        if (valeur.IsFailure)
            return Result.Failure<decimal>(valeur.Error);

        if (valeur.Value is null)
            return Result.Success(defaut);

        if (decimal.TryParse(valeur.Value, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var result))
            return Result.Success(result);

        // Valeur corrompue → défaut plutôt qu'échec (robustesse d'exploitation).
        return Result.Success(defaut);
    }

    public async Task<Result<decimal>> LireDecimalObligatoireAsync(string cle, string dateEffet, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cle);

        var valeur = await LireValeurAsync(cle, dateEffet, ct);
        if (valeur.IsFailure)
            return Result.Failure<decimal>(valeur.Error);

        if (valeur.Value is null)
            return Result.Failure<decimal>(Error.NotFound($"Paramètre obligatoire « {cle} » absent de la table Parametres à la date {dateEffet}."));

        if (decimal.TryParse(valeur.Value, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var result))
            return Result.Success(result);

        return Result.Failure<decimal>(Error.Validation($"Paramètre « {cle} » : valeur « {valeur.Value} » non décimale."));
    }
}
