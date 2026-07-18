using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Formules;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Common;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Infrastructure.Repositories.Payroll;

/// <summary>
/// Écriture du référentiel des rubriques (C4.1) : création/édition d'une
/// rubrique, de sa formule versionnée, de ses paramètres versionnés et de ses
/// dépendances (graphe DAG). Dapper, lecture-écriture. La formule est validée
/// par le <see cref="FormulaParser"/> avant persistance ; les dépendances
/// refusent les cycles.
/// </summary>
public sealed class RubriqueRepository : IRubriqueRepository
{
    private static readonly HashSet<string> NaturesAutorisees =
        new(StringComparer.Ordinal) { "GAIN", "RETENUE", "COTISATION", "IMPOT" };

    private static readonly HashSet<string> BasesCalculAutorisees =
        new(StringComparer.Ordinal)
        {
            "TRAITEMENT", "TBASE", "TBASE_ECHELON", "INDICE_ECHELON",
            "FORFAIT", "ASSIETTE_COTISABLE", "ASSIETTE_IMPOSABLE",
        };

    private static readonly HashSet<string> PeriodicitesAutorisees =
        new(StringComparer.Ordinal) { "MENSUELLE", "TRIMESTRIELLE", "ANNUELLE", "PONCTUELLE" };

    private readonly SqliteConnection _connection;
    private readonly ICacheInvalidator? _cache;

    public RubriqueRepository(SqliteConnection connection, ICacheInvalidator? cache = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _cache = cache;
    }

    public async Task<Result<string>> DefinirRubriqueAsync(
        string id, string libelle, string nature, string baseCalcul, string periodicite,
        string? periodiciteVersement, int ordreCalcul, bool estImposable, bool estCotisable,
        string description, bool estAffectableManuellement, bool occurrencesMultiples,
        string? sourceValeurId, string? source, DateTimeOffset creeLe, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Result.Failure<string>(Error.Validation("L'identifiant de la rubrique est requis."));
        if (string.IsNullOrWhiteSpace(libelle))
            return Result.Failure<string>(Error.Validation("Le libellé de la rubrique est requis."));
        if (!NaturesAutorisees.Contains(nature))
            return Result.Failure<string>(Error.Validation($"Nature « {nature} » non autorisée."));
        if (!BasesCalculAutorisees.Contains(baseCalcul))
            return Result.Failure<string>(Error.Validation($"BaseCalcul « {baseCalcul} » non autorisée."));
        if (!PeriodicitesAutorisees.Contains(periodicite))
            return Result.Failure<string>(Error.Validation($"Periodicité « {periodicite} » non autorisée."));
        if (periodiciteVersement is not null && !PeriodicitesAutorisees.Contains(periodiciteVersement))
            return Result.Failure<string>(Error.Validation($"PeriodicitéVersement « {periodiciteVersement} » non autorisée."));
        if (ordreCalcul < 0)
            return Result.Failure<string>(Error.Validation("L'ordre de calcul doit être positif ou nul."));

        var existe = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("SELECT Id FROM Rubriques WHERE Id = @id;", new { id }, cancellationToken: ct));

        var createdAt = creeLe.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        if (existe is null)
        {
            await _connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO Rubriques
                    (Id, Libelle, Nature, BaseCalcul, Periodicite, PeriodiciteVersement,
                     OrdreCalcul, EstImposable, EstCotisable, Description, Actif,
                     CreatedAt, Source, Hash, EstAffectableManuellement, OccurrencesMultiples, SourceValeurId)
                VALUES
                    (@id, @l, @n, @b, @p, @pv, @o, @ei, @ec, @d, 1,
                     @createdAt, @src, @hash, @eam, @om, @sv);
                """,
                new
                {
                    id, l = libelle, n = nature, b = baseCalcul, p = periodicite,
                    pv = periodiciteVersement ?? (object)DBNull.Value, o = ordreCalcul,
                    ei = estImposable ? 1 : 0, ec = estCotisable ? 1 : 0, d = description,
                    createdAt, src = source ?? (object)DBNull.Value, hash = $"h-rubrique-{id}",
                    eam = estAffectableManuellement ? 1 : 0, om = occurrencesMultiples ? 1 : 0,
                    sv = sourceValeurId ?? (object)DBNull.Value,
                }, cancellationToken: ct));
            _cache?.Invalider();
            return Result.Success(id);
        }

        await _connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Rubriques
            SET Libelle = @l, Nature = @n, BaseCalcul = @b, Periodicite = @p,
                PeriodiciteVersement = @pv, OrdreCalcul = @o, EstImposable = @ei,
                EstCotisable = @ec, Description = @d, UpdatedAt = @updatedAt,
                Source = @src, Hash = @hash, EstAffectableManuellement = @eam,
                OccurrencesMultiples = @om, SourceValeurId = @sv
            WHERE Id = @id;
            """,
            new
            {
                id, l = libelle, n = nature, b = baseCalcul, p = periodicite,
                pv = periodiciteVersement ?? (object)DBNull.Value, o = ordreCalcul,
                ei = estImposable ? 1 : 0, ec = estCotisable ? 1 : 0, d = description,
                updatedAt = createdAt, src = source ?? (object)DBNull.Value, hash = $"h-rubrique-{id}",
                eam = estAffectableManuellement ? 1 : 0, om = occurrencesMultiples ? 1 : 0,
                sv = sourceValeurId ?? (object)DBNull.Value,
            }, cancellationToken: ct));
        _cache?.Invalider();
        return Result.Success(id);
    }

    public async Task<Result<string>> DefinirFormuleAsync(
        string rubriqueId, string expression, string dateEffet, int ordre,
        string? source, DateTimeOffset creeLe, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rubriqueId))
            return Result.Failure<string>(Error.Validation("L'identifiant de la rubrique est requis."));
        Guard.AgainstNullOrWhiteSpace(expression);
        Guard.AgainstNullOrWhiteSpace(dateEffet);

        var parse = FormulaParser.Parser(expression);
        if (parse.IsFailure)
            return Result.Failure<string>(parse.Error);

        var rubriqueExiste = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("SELECT Id FROM Rubriques WHERE Id = @id;", new { id = rubriqueId }, cancellationToken: ct));
        if (rubriqueExiste is null)
            return Result.Failure<string>(Error.NotFound($"La rubrique « {rubriqueId} » n'existe pas."));

        var existeDeja = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                "SELECT Id FROM RubriqueFormules WHERE RubriqueId = @r AND DateEffet = @dateEffet;",
                new { r = rubriqueId, dateEffet }, cancellationToken: ct));
        if (existeDeja is not null)
            return Result.Failure<string>(
                Error.Conflict($"Une formule est déjà définie pour « {rubriqueId} » à la date {dateEffet}."));

        var courante = await _connection.QuerySingleOrDefaultAsync<VersionCourante>(
            new CommandDefinition(
                "SELECT Id, DateEffet FROM RubriqueFormules WHERE RubriqueId = @r AND DateFin IS NULL;",
                new { r = rubriqueId }, cancellationToken: ct));
        var erreurContinuite = ValiderContinuite(courante, dateEffet);
        if (erreurContinuite is not null)
            return Result.Failure<string>(erreurContinuite);

        var id = $"RF-{rubriqueId}-{dateEffet}";
        var createdAt = creeLe.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        using var tx = _connection.BeginTransaction();
        if (courante is not null)
            await FermerVersionAsync("RubriqueFormules", courante.Id, dateEffet, tx, ct);

        await _connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO RubriqueFormules
                (Id, RubriqueId, DateEffet, DateFin, Expression, Ordre, Source, Hash, CreatedAt)
            VALUES (@id, @r, @dateEffet, NULL, @expr, @ordre, @src, @hash, @createdAt);
            """,
            new
            {
                id, r = rubriqueId, dateEffet, expr = expression, ordre,
                src = source ?? (object)DBNull.Value, hash = $"h-{id}", createdAt,
            }, tx, cancellationToken: ct));

        tx.Commit();
        _cache?.Invalider();
        return Result.Success(id);
    }

    public async Task<Result<string>> DefinirParametreAsync(
        string rubriqueId, string cle, string valeur, string dateEffet,
        string? source, DateTimeOffset creeLe, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rubriqueId))
            return Result.Failure<string>(Error.Validation("L'identifiant de la rubrique est requis."));
        Guard.AgainstNullOrWhiteSpace(cle);
        Guard.AgainstNullOrWhiteSpace(valeur);
        Guard.AgainstNullOrWhiteSpace(dateEffet);

        var rubriqueExiste = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("SELECT Id FROM Rubriques WHERE Id = @id;", new { id = rubriqueId }, cancellationToken: ct));
        if (rubriqueExiste is null)
            return Result.Failure<string>(Error.NotFound($"La rubrique « {rubriqueId} » n'existe pas."));

        var existeDeja = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                "SELECT Id FROM RubriqueParametres WHERE RubriqueId = @r AND Cle = @cle AND DateEffet = @dateEffet;",
                new { r = rubriqueId, cle, dateEffet }, cancellationToken: ct));
        if (existeDeja is not null)
            return Result.Failure<string>(
                Error.Conflict($"Un paramètre « {cle} » est déjà défini pour « {rubriqueId} » à la date {dateEffet}."));

        var courant = await _connection.QuerySingleOrDefaultAsync<VersionCourante>(
            new CommandDefinition(
                "SELECT Id, DateEffet FROM RubriqueParametres WHERE RubriqueId = @r AND Cle = @cle AND DateFin IS NULL;",
                new { r = rubriqueId, cle }, cancellationToken: ct));
        var erreurContinuite = ValiderContinuite(courant, dateEffet);
        if (erreurContinuite is not null)
            return Result.Failure<string>(erreurContinuite);

        var id = $"RP-{rubriqueId}-{cle}-{dateEffet}";
        var createdAt = creeLe.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        using var tx = _connection.BeginTransaction();
        if (courant is not null)
            await FermerVersionAsync("RubriqueParametres", courant.Id, dateEffet, tx, ct);

        await _connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO RubriqueParametres
                (Id, RubriqueId, Cle, DateEffet, DateFin, Valeur, Source, Hash, CreatedAt)
            VALUES (@id, @r, @cle, @dateEffet, NULL, @valeur, @src, @hash, @createdAt);
            """,
            new
            {
                id, r = rubriqueId, cle, dateEffet, valeur,
                src = source ?? (object)DBNull.Value, hash = $"h-{id}", createdAt,
            }, tx, cancellationToken: ct));

        tx.Commit();
        _cache?.Invalider();
        return Result.Success(id);
    }

    public async Task<Result<string>> DefinirDependanceAsync(
        string rubriqueId, string dependDeId, string dateEffet,
        string? source, DateTimeOffset creeLe, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rubriqueId) || string.IsNullOrWhiteSpace(dependDeId))
            return Result.Failure<string>(Error.Validation("Les identifiants de rubriques sont requis."));
        Guard.AgainstNullOrWhiteSpace(dateEffet);
        if (string.Equals(rubriqueId, dependDeId, StringComparison.Ordinal))
            return Result.Failure<string>(Error.Validation("Une rubrique ne peut pas dépendre d'elle-même."));

        var rubriqueExiste = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("SELECT Id FROM Rubriques WHERE Id = @id;", new { id = rubriqueId }, cancellationToken: ct));
        if (rubriqueExiste is null)
            return Result.Failure<string>(Error.NotFound($"La rubrique « {rubriqueId} » n'existe pas."));
        var dependExiste = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("SELECT Id FROM Rubriques WHERE Id = @id;", new { id = dependDeId }, cancellationToken: ct));
        if (dependExiste is null)
            return Result.Failure<string>(Error.NotFound($"La rubrique « {dependDeId} » n'existe pas."));

        var cycle = await DetecterCycleAsync(rubriqueId, dependDeId, ct);
        if (cycle.IsFailure)
            return Result.Failure<string>(cycle.Error);

        var existeDeja = await _connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                "SELECT Id FROM RubriqueDependances WHERE RubriqueId = @r AND DependDeId = @d AND DateEffet = @dateEffet;",
                new { r = rubriqueId, d = dependDeId, dateEffet }, cancellationToken: ct));
        if (existeDeja is not null)
            return Result.Failure<string>(
                Error.Conflict($"Une dépendance « {rubriqueId} → {dependDeId} » existe déjà à la date {dateEffet}."));

        var courante = await _connection.QuerySingleOrDefaultAsync<VersionCourante>(
            new CommandDefinition(
                "SELECT Id, DateEffet FROM RubriqueDependances WHERE RubriqueId = @r AND DependDeId = @d AND DateFin IS NULL;",
                new { r = rubriqueId, d = dependDeId }, cancellationToken: ct));
        var erreurContinuite = ValiderContinuite(courante, dateEffet);
        if (erreurContinuite is not null)
            return Result.Failure<string>(erreurContinuite);

        var id = $"RD-{rubriqueId}-{dependDeId}-{dateEffet}";
        var createdAt = creeLe.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        using var tx = _connection.BeginTransaction();
        if (courante is not null)
            await FermerVersionAsync("RubriqueDependances", courante.Id, dateEffet, tx, ct);

        await _connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO RubriqueDependances
                (Id, RubriqueId, DependDeId, DateEffet, DateFin, Source, Hash, CreatedAt)
            VALUES (@id, @r, @d, @dateEffet, NULL, @src, @hash, @createdAt);
            """,
            new
            {
                id, r = rubriqueId, d = dependDeId, dateEffet,
                src = source ?? (object)DBNull.Value, hash = $"h-{id}", createdAt,
            }, tx, cancellationToken: ct));

        tx.Commit();
        _cache?.Invalider();
        return Result.Success(id);
    }

    // ----------------------------------------------------------------- garde-fous

    private async Task<Result<bool>> DetecterCycleAsync(string rubriqueId, string nouvelleDependance, CancellationToken ct)
    {
        // Une dépendance A→B crée un cycle si B (transitivement) dépend déjà de A.
        var visite = nouvelleDependance;
        var dejaVu = new HashSet<string>(StringComparer.Ordinal);
        while (visite is not null)
        {
            if (string.Equals(visite, rubriqueId, StringComparison.Ordinal))
                return Result.Failure<bool>(Error.Cycle(
                    $"Dépendance refusée : cycle détecté ({rubriqueId} → … → {nouvelleDependance} → {rubriqueId})."));
            if (!dejaVu.Add(visite))
                break;
            visite = await _connection.QuerySingleOrDefaultAsync<string>(
                new CommandDefinition(
                    "SELECT DependDeId FROM RubriqueDependances WHERE RubriqueId = @r AND DateFin IS NULL LIMIT 1;",
                    new { r = visite }, cancellationToken: ct));
        }
        return Result.Success(true);
    }

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
