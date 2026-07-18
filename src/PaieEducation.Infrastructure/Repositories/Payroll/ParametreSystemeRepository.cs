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

        // Lot 1.1 : strict. Absence ou corruption = échec explicite.
        // Le seed ReglementaireSeeder garantit la présence d'ARRONDI_MODE
        // depuis 2007-01-01 ; une absence/corruption est un signal de
        // mauvaise configuration à corriger, pas à masquer.
        if (valeur.Value is null)
            return Result.Failure<ModeArrondi>(Error.NotFound(
                $"Paramètre obligatoire « ARRONDI_MODE » absent de la table Parametres à la date {dateEffet}."));

        var mode = ArrondiService.ParserMode(valeur.Value);
        if (mode.IsFailure)
            return Result.Failure<ModeArrondi>(mode.Error);

        return Result.Success(mode.Value);
    }

    public async Task<Result<decimal>> LireDecimalOuDefautAsync(string cle, decimal defaut, string dateEffet, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cle);

        // Lot 1.1 / Option B1 : helper pour paramètres NON critiques.
        // Le nom explicite "OuDefaut" verrouille le contrat "valeur par
        // défaut acceptée". Tout paramètre métier dont l'absence doit
        // bloquer le calcul doit passer par LireDecimalObligatoireAsync.
        var valeur = await LireValeurAsync(cle, dateEffet, ct);
        if (valeur.IsFailure)
            return Result.Failure<decimal>(valeur.Error);

        if (valeur.Value is null)
            return Result.Success(defaut);

        if (decimal.TryParse(valeur.Value, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var result))
            return Result.Success(result);

        // Valeur corrompue → défaut (helper non-critique, on ne bloque pas le calcul).
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
