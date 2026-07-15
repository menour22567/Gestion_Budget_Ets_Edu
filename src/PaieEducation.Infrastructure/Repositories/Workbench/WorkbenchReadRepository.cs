using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Infrastructure.Repositories.Workbench;

/// <summary>
/// Repository lecture-seule pour les données du Workbench réglementaire
/// (V008 § 8bis.1, V009 § 8ter). Dapper sur SqliteConnection. Pas d'écriture
/// (les modifications passent par l'UI Workbench Phase 6 ou le seeder CLI).
/// </summary>
/// <remarks>
/// Toutes les méthodes sont asynchrones mais utilisent <c>SqliteConnection</c>
/// synchrone (Microsoft.Data.Sqlite). Le <c>using async</c> est conservé pour
/// la compatibilité future avec un driver async natif (ADR-0005).
/// </remarks>
public sealed class WorkbenchReadRepository
{
    private readonly SqliteConnection _connection;

    public WorkbenchReadRepository(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    // -----------------------------------------------------------------------
    // Catalogues V009
    // -----------------------------------------------------------------------

    public async Task<IReadOnlyList<SourceValeur>> ListerSourcesValeurAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, Libelle, Description, Actif, CreatedAt, CreatedBy
            FROM SourcesValeur
            WHERE Actif = 1
            ORDER BY Id;
            """;
        var rows = await _connection.QueryAsync<SourceRow>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.Select(MapSource).ToList();
    }

    public async Task<IReadOnlyDictionary<string, CritereEligibilite>> ListerCriteresParIdAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, Libelle, TypeValeur, SourceResolution, Actif, CreatedAt, CreatedBy
            FROM CriteresEligibilite
            WHERE Actif = 1;
            """;
        var rows = await _connection.QueryAsync<CritereRow>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToDictionary(r => r.Id, MapCritere);
    }

    public async Task<IReadOnlyList<MessageRegle>> ListerMessagesReglesActifsAsync(
        string datePaie, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, Categorie, TexteFr, TexteAr, Source, DateEffet, DateFin
            FROM MessagesRegles
            WHERE Actif = 1
              AND DateEffet <= @date
              AND (DateFin IS NULL OR DateFin >= @date)
            ORDER BY Priorite DESC, Id;
            """;
        var rows = await _connection.QueryAsync<MessageRow>(new CommandDefinition(
            sql, new { date = datePaie }, cancellationToken: ct));
        return rows.Select(MapMessage).ToList();
    }

    public async Task<IReadOnlyList<GroupeEligibilite>> ListerGroupesParRubriqueAsync(
        string rubriqueId, string datePaie, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, RubriqueId, Severite, MessageId, Priorite, DateEffet, DateFin, Source
            FROM GroupesEligibilite
            WHERE RubriqueId = @rub
              AND DateEffet <= @date
              AND (DateFin IS NULL OR DateFin >= @date)
            ORDER BY Priorite, Id;
            """;
        var rows = await _connection.QueryAsync<GroupeRow>(new CommandDefinition(
            sql, new { rub = rubriqueId, date = datePaie }, cancellationToken: ct));
        return rows.Select(MapGroupe).ToList();
    }

    // -----------------------------------------------------------------------
    // Données V008 / V004 / V005 utilisées par le Workbench
    // -----------------------------------------------------------------------

    /// <summary>
    /// Tous les barèmes d'une rubrique, indépendamment de la dimension. Le
    /// résolveur côté Domain (BaremeResolver) appliquera le filtre dimension/clé/date.
    /// </summary>
    public async Task<IReadOnlyList<BaremeValue>> ListerBaremesRubriqueAsync(
        string rubriqueId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, RubriqueId, Dimension, BorneInf, BorneSup, TypeValeur, Valeur,
                   DateEffet, DateFin
            FROM RubriqueBaremes
            WHERE RubriqueId = @rub
            ORDER BY Dimension, BorneInf, DateEffet;
            """;
        var rows = await _connection.QueryAsync<BaremeRow>(new CommandDefinition(
            sql, new { rub = rubriqueId }, cancellationToken: ct));
        return rows.Select(MapBareme).ToList();
    }

    public async Task<IReadOnlyList<ConditionEligibilite>> ListerConditionsParRubriqueAsync(
        string rubriqueId, string datePaie, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, RubriqueId, CritereId, GroupeId, Operateur, Valeur, DateEffet, DateFin
            FROM ReglesEligibilite
            WHERE RubriqueId = @rub
              AND DateEffet <= @date
              AND (DateFin IS NULL OR DateFin >= @date)
            ORDER BY Id;
            """;
        var rows = await _connection.QueryAsync<ConditionRow>(new CommandDefinition(
            sql, new { rub = rubriqueId, date = datePaie }, cancellationToken: ct));
        return rows.Select(MapCondition).ToList();
    }

    /// <summary>
    /// Charge les périodes existantes d'une rubrique, toutes dimensions/barèmes
    /// confondus, pour le calcul de continuité temporelle (L-U8).
    /// </summary>
    public async Task<IReadOnlyList<PeriodeReglementaire>> ListerPeriodesRubriqueAsync(
        string rubriqueId, CancellationToken ct = default)
    {
        const string sqlBareme = """
            SELECT DISTINCT DateEffet, DateFin FROM RubriqueBaremes
            WHERE RubriqueId = @rub
            UNION
            SELECT DISTINCT DateEffet, DateFin FROM ReglesEligibilite
            WHERE RubriqueId = @rub
            ORDER BY DateEffet;
            """;
        var rows = await _connection.QueryAsync<PeriodeRow>(new CommandDefinition(
            sqlBareme, new { rub = rubriqueId }, cancellationToken: ct));
        return rows.Select(r => PeriodeReglementaire.Creer(r.DateEffet, r.DateFin)).ToList();
    }

    // -----------------------------------------------------------------------
    // Mappers (SQL row → Value Object)
    // -----------------------------------------------------------------------

    private sealed record SourceRow(string Id, string Libelle, string? Description,
        long Actif, string CreatedAt, string CreatedBy);

    private sealed record CritereRow(string Id, string Libelle, string TypeValeur,
        string SourceResolution, long Actif, string CreatedAt, string CreatedBy);

    private sealed record MessageRow(string Id, string Categorie, string TexteFr,
        string? TexteAr, string Source, string DateEffet, string? DateFin);

    private sealed record GroupeRow(string Id, string RubriqueId, string Severite,
        string? MessageId, long Priorite, string DateEffet, string? DateFin, string? Source);

    private sealed record BaremeRow(string Id, string RubriqueId, string Dimension,
        string BorneInf, string? BorneSup, string TypeValeur, string Valeur,
        string DateEffet, string? DateFin);

    private sealed record ConditionRow(string Id, string RubriqueId, string CritereId,
        string? GroupeId, string Operateur, string Valeur, string DateEffet, string? DateFin);

    private sealed record PeriodeRow(string DateEffet, string? DateFin);

    private static SourceValeur MapSource(SourceRow r)
        => SourceValeur.Creer(r.Id, r.Libelle, r.Description);

    private static CritereEligibilite MapCritere(CritereRow r)
        => CritereEligibilite.Creer(r.Id, r.Libelle, ParseTypeValeur(r.TypeValeur),
            ParseSourceResolution(r.SourceResolution));

    private static MessageRegle MapMessage(MessageRow r)
        => MessageRegle.Creer(r.Id, ParseCategorie(r.Categorie), r.TexteFr, r.TexteAr,
            r.Source, PeriodeReglementaire.Creer(r.DateEffet, r.DateFin));

    private static GroupeEligibilite MapGroupe(GroupeRow r)
        => GroupeEligibilite.Creer(r.Id, r.RubriqueId, ParseSeverite(r.Severite),
            r.MessageId, checked((int)r.Priorite), PeriodeReglementaire.Creer(r.DateEffet, r.DateFin), r.Source);

    private static BaremeValue MapBareme(BaremeRow r)
        => BaremeValue.Creer(r.RubriqueId, ParseDimension(r.Dimension), r.BorneInf,
            r.BorneSup, ParseTypeValeurBareme(r.TypeValeur), r.Valeur,
            PeriodeReglementaire.Creer(r.DateEffet, r.DateFin));

    private static ConditionEligibilite MapCondition(ConditionRow r)
        => ConditionEligibilite.Creer(r.Id, r.RubriqueId, r.CritereId,
            ParseOperateur(r.Operateur), r.Valeur, r.GroupeId,
            PeriodeReglementaire.Creer(r.DateEffet, r.DateFin));

    private static TypeValeurCritere ParseTypeValeur(string s) => s switch
    {
        "TEXT" => TypeValeurCritere.Text,
        "INT" => TypeValeurCritere.Int,
        "DATE" => TypeValeurCritere.Date,
        "ENUM" => TypeValeurCritere.Enum,
        _ => throw new InvalidOperationException($"TypeValeur inconnu en base : {s}")
    };

    private static SourceResolution ParseSourceResolution(string s) => s switch
    {
        "ATTRIBUT_AGENT" => SourceResolution.AttributAgent,
        "ATTRIBUT_GRADE" => SourceResolution.AttributGrade,
        "CARRIERE" => SourceResolution.Carriere,
        "CALCULE" => SourceResolution.Calcule,
        _ => throw new InvalidOperationException($"SourceResolution inconnue : {s}")
    };

    private static Severite ParseSeverite(string s) => s switch
    {
        "INFO" => Severite.Info,
        "RECOMMANDEE" => Severite.Recommandee,
        "OBLIGATOIRE_REGLEMENTAIRE" => Severite.ObligatoireReglementaire,
        _ => throw new InvalidOperationException($"Severite inconnue : {s}")
    };

    private static MessageCategorie ParseCategorie(string s) => s switch
    {
        "ELIGIBILITE" => MessageCategorie.Eligibilite,
        "AVERTISSEMENT" => MessageCategorie.Avertissement,
        "SUGGESTION" => MessageCategorie.Suggestion,
        _ => throw new InvalidOperationException($"MessageCategorie inconnue : {s}")
    };

    private static BaremeDimension ParseDimension(string s) => s switch
    {
        "CATEGORIE" => BaremeDimension.Categorie,
        "ECHELON" => BaremeDimension.Echelon,
        "ANCIENNETE" => BaremeDimension.Anciennete,
        "TYPE_ETABLISSEMENT" => BaremeDimension.TypeEtablissement,
        "CORPS" => BaremeDimension.Corps,
        "GRADE" => BaremeDimension.Grade,
        _ => throw new InvalidOperationException($"BaremeDimension inconnue : {s}")
    };

    private static BaremeTypeValeur ParseTypeValeurBareme(string s) => s switch
    {
        "TAUX" => BaremeTypeValeur.Taux,
        "MONTANT" => BaremeTypeValeur.Montant,
        _ => throw new InvalidOperationException($"BaremeTypeValeur inconnue : {s}")
    };

    private static Operateur ParseOperateur(string s) => s switch
    {
        "=" => Operateur.Egal,
        "IN" => Operateur.In,
        "NOT_IN" => Operateur.NotIn,
        ">=" => Operateur.SuperieurEgal,
        "<=" => Operateur.InferieurEgal,
        ">" => Operateur.Superieur,
        "<" => Operateur.Inferieur,
        _ => throw new InvalidOperationException($"Operateur inconnu : {s}")
    };
}
