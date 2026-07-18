using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Infrastructure.Repositories.Payroll;

/// <summary>
/// Implémentation SQLite de <see cref="IRubriqueParametreLookup"/>. Lit
/// <c>RubriqueParametres</c> à la date d'effet, par clé. Lot 1.2 V1 : pas
/// de filtre par rubrique (le contexte n'est pas encore propagé jusqu'au
/// calculateur) — si la même <c>Cle</c> est utilisée par plusieurs rubriques,
/// on prend la version la plus récente. À durcir dans un chantier ultérieur
/// (rubrique contextuelle).
/// </summary>
public sealed class RubriqueParametreLookup : IRubriqueParametreLookup
{
    private readonly SqliteConnection _connection;

    public RubriqueParametreLookup(SqliteConnection connection)
        => _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public async Task<Result<decimal>> LireParametreAsync(string cle, string dateEffet, CancellationToken ct = default)
    {
        return await LireParametreAvecOverridesAsync(cle, dateEffet, overrides: null, ct);
    }

    public async Task<Result<decimal>> LireParametreAvecOverridesAsync(
        string cle, string dateEffet, IReadOnlyDictionary<string, decimal>? overrides, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cle);
        ArgumentException.ThrowIfNullOrWhiteSpace(dateEffet);

        // Lot 3.3 — J5N §2.3 (D-P2) : override > DB. Si la clé est dans le
        // dictionnaire d'overrides, on retourne la valeur surchargée sans
        // toucher la base. Sinon, lecture DB normale (cf. LireParametreAsync).
        if (overrides is not null && overrides.TryGetValue(cle, out var vpi))
        {
            return Result.Success(vpi);
        }

        // Lecture brute : on récupère toutes les versions en vigueur à la
        // date. Si la Cle n'est pas unique, on prend la plus récente (DateEffet
        // le plus grand). La conversion est en invariant pour ne pas piéger
        // les locales francophones.
        const string sql = """
            SELECT Valeur
            FROM RubriqueParametres
            WHERE Cle = @cle
              AND DateEffet <= @date
              AND (DateFin IS NULL OR DateFin >= @date)
            ORDER BY DateEffet DESC
            LIMIT 1;
            """;
        var valeur = await _connection.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(sql, new { cle, date = dateEffet }, cancellationToken: ct));

        if (valeur is null)
            return Result.Failure<decimal>(Error.NotFound(
                $"Paramètre de rubrique « {cle} » absent à la date {dateEffet}."));

        if (!decimal.TryParse(valeur, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var result))
            return Result.Failure<decimal>(Error.Validation(
                $"Paramètre de rubrique « {cle} » : valeur « {valeur} » non décimale."));

        return Result.Success(result);
    }
}
