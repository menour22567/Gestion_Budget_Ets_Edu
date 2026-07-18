using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Cotisations;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Domain.Calcul.ValueObjects;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Workbench.Constants;
using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Infrastructure.Repositories.Payroll;

/// <summary>
/// Charge le <see cref="PayrollInput"/> d'un calcul de bulletin depuis SQLite
/// (formules, barèmes, règles d'éligibilité, cotisations, règle IRG). Lecture
/// seule, Dapper.
/// </summary>
/// <remarks>
/// Les variables de base (<c>INDICE_MIN</c>, <c>INDICE_ECH</c>, <c>VPI</c>,
/// <c>TBASE</c>, <c>TRT</c>, <c>ECH</c>, <c>CAT</c>) et les sources de valeur
/// (notation) sont fournies par l'appelant : leur résolution depuis la grille
/// indiciaire et le dossier agent relève de la Phase 5 (la table <c>Agents</c>
/// n'existe pas encore). Ce repository prouve le cœur de la Phase 4 : les
/// <b>formules réglementaires sont lues en base</b>, pas codées en dur.
/// </remarks>
public sealed class PayrollReadRepository : IPayrollReadRepository
{
    private readonly SqliteConnection _connection;

    public PayrollReadRepository(SqliteConnection connection)
        => _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public async Task<Result<PayrollInput>> ChargerAsync(
        AgentContext agent,
        string datePaie,
        IReadOnlyDictionary<string, decimal> variablesBase,
        IReadOnlyDictionary<string, decimal> sourcesValeur,
        IReadOnlyDictionary<string, string> clesBareme,
        ProfilFiscal profil,
        CancellationToken ct = default)
    {
        return await ChargerAvecBaremesOverrideAsync(agent, datePaie, variablesBase, sourcesValeur, clesBareme,
            profil, baremesOverride: null, ct);
    }

    public async Task<Result<PayrollInput>> ChargerAvecBaremesOverrideAsync(
        AgentContext agent,
        string datePaie,
        IReadOnlyDictionary<string, decimal> variablesBase,
        IReadOnlyDictionary<string, decimal> sourcesValeur,
        IReadOnlyDictionary<string, string> clesBareme,
        ProfilFiscal profil,
        IReadOnlyList<BaremeValue>? baremesOverride,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(variablesBase);

        var rubriques = await ChargerRubriquesGainAsync(datePaie, ct);
        var baremesDb = await ChargerBaremesAsync(rubriques.Select(r => r.Id).ToList(), ct);

        // Agrégation DB + overrides (Lot 3.2). Les overrides sont insérés en
        // tête de liste : en cas d'égalité (RubriqueId, Dimension, BorneInf,
        // période couvrante), la première occurrence gagne → override > DB.
        // Cf. J5M §3 (D-B4) « premier override gagne ». Règle pragmatique,
        // testable, documentée.
        IReadOnlyList<BaremeValue> baremes = baremesOverride is { Count: > 0 }
            ? baremesOverride.Concat(baremesDb).ToList()
            : baremesDb;

        var conditions = await ChargerConditionsAsync(datePaie, ct);
        var criteres = await ChargerCriteresAsync(ct);
        var cotisations = await ChargerCotisationsAsync(datePaie, ct);
        // Lot 2.1 — arêtes actives du DAG à la date de paie. Une dépendance
        // expirée (DateFin < datePaie) n'est jamais chargée : elle n'a pas
        // d'effet à la date considérée. Une dépendance vers une rubrique
        // hors univers (pas dans la liste ci-dessus) sera détectée par le
        // DependencyResolver (échec Validation explicite) — pas besoin
        // d'un filtrage en amont.
        var dependances = await ChargerDependancesAsync(datePaie, ct);

        var regleIrg = await ChargerRegleIrgAsync(datePaie, ct);
        if (regleIrg.IsFailure)
            return Result.Failure<PayrollInput>(regleIrg.Error);

        return Result.Success(new PayrollInput(
            agent, datePaie, variablesBase, sourcesValeur, clesBareme,
            rubriques, baremes, conditions, criteres, cotisations, profil, regleIrg.Value,
            dependances));
    }

    // ---- Rubriques GAIN ayant une formule active à la date ----

    private async Task<IReadOnlyList<RubriqueCalcul>> ChargerRubriquesGainAsync(string date, CancellationToken ct)
    {
        const string sql = """
            SELECT r.Id, r.Nature, r.EstImposable, r.EstCotisable, r.OrdreCalcul, f.Expression
            FROM Rubriques r
            JOIN RubriqueFormules f ON f.Id = (
                SELECT f2.Id FROM RubriqueFormules f2
                WHERE f2.RubriqueId = r.Id
                  AND f2.DateEffet <= @date
                  AND (f2.DateFin IS NULL OR f2.DateFin >= @date)
                ORDER BY f2.DateEffet DESC LIMIT 1)
            WHERE r.Actif = 1 AND r.Nature = 'GAIN'
            ORDER BY r.OrdreCalcul, r.Id;
            """;
        var rows = await _connection.QueryAsync<RubriqueRow>(new CommandDefinition(sql, new { date }, cancellationToken: ct));
        return rows.Select(r => new RubriqueCalcul(
            r.Id, NatureRubrique.Gain, r.Expression, r.EstImposable == 1, r.EstCotisable == 1, checked((int)r.OrdreCalcul)))
            .ToList();
    }

    private async Task<IReadOnlyList<BaremeValue>> ChargerBaremesAsync(IReadOnlyList<string> rubriqueIds, CancellationToken ct)
    {
        if (rubriqueIds.Count == 0) return Array.Empty<BaremeValue>();
        const string sql = """
            SELECT Id, RubriqueId, Dimension, BorneInf, BorneSup, TypeValeur, Valeur, DateEffet, DateFin
            FROM RubriqueBaremes
            WHERE RubriqueId IN @ids
            ORDER BY RubriqueId, Dimension, BorneInf, DateEffet;
            """;
        var rows = await _connection.QueryAsync<BaremeRow>(new CommandDefinition(sql, new { ids = rubriqueIds }, cancellationToken: ct));
        return rows.Select(r => BaremeValue.Creer(
            r.RubriqueId, ParseDimension(r.Dimension), r.BorneInf, r.BorneSup,
            ParseTypeValeurBareme(r.TypeValeur), r.Valeur,
            PeriodeReglementaire.Creer(r.DateEffet, r.DateFin))).ToList();
    }

    private async Task<IReadOnlyList<ConditionEligibilite>> ChargerConditionsAsync(string date, CancellationToken ct)
    {
        const string sql = """
            SELECT Id, RubriqueId, CritereId, GroupeId, Operateur, Valeur, DateEffet, DateFin
            FROM ReglesEligibilite
            WHERE DateEffet <= @date AND (DateFin IS NULL OR DateFin >= @date)
            ORDER BY Id;
            """;
        var rows = await _connection.QueryAsync<ConditionRow>(new CommandDefinition(sql, new { date }, cancellationToken: ct));
        return rows.Select(r => ConditionEligibilite.Creer(
            r.Id, r.RubriqueId, r.CritereId, ParseOperateur(r.Operateur), r.Valeur, r.GroupeId,
            PeriodeReglementaire.Creer(r.DateEffet, r.DateFin))).ToList();
    }

    private async Task<IReadOnlyDictionary<string, CritereEligibilite>> ChargerCriteresAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT Id, Libelle, TypeValeur, SourceResolution
            FROM CriteresEligibilite WHERE Actif = 1;
            """;
        var rows = await _connection.QueryAsync<CritereRow>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToDictionary(
            r => r.Id,
            r => CritereEligibilite.Creer(r.Id, r.Libelle, ParseTypeValeur(r.TypeValeur), ParseSourceResolution(r.SourceResolution)));
    }

    private async Task<IReadOnlyList<CotisationCalcul>> ChargerCotisationsAsync(string date, CancellationToken ct)
    {
        // Cotisations proportionnelles actives (les facultatives à montant fixe
        // dépendent du choix de l'agent — Phase 5).
        const string sql = """
            SELECT Code, AssietteRef, Taux, TypeCotisation
            FROM Cotisations
            WHERE AssietteRef <> 'MONTANT_FIXE'
              AND DateEffet <= @date AND (DateFin IS NULL OR DateFin >= @date)
            ORDER BY Code;
            """;
        var rows = await _connection.QueryAsync<CotisationRow>(new CommandDefinition(sql, new { date }, cancellationToken: ct));
        return rows.Select(r => new CotisationCalcul(
            new CotisationDef(r.Code, ParseAssiette(r.AssietteRef), (decimal?)r.Taux, MontantFixe: null),
            EstSalariale: r.TypeCotisation == "OBLIGATOIRE_SALARIALE")).ToList();
    }

    // ---- Dépendances entre rubriques (Lot 2.1) ----

    /// <summary>
    /// Charge les arêtes actives du graphe DAG de calcul à la date de paie.
    /// Filtre point-in-time : <c>DateEffet &lt;= date</c> et
    /// <c>(DateFin IS NULL OR DateFin &gt;= date)</c>. Une arête expirée
    /// (par exemple, dépendance abandonnée lors d'une refonte) est silencieusement
    /// omise — le pipeline retombe sur l'ordre naturel <c>(OrdreCalcul, Id)</c>.
    /// </summary>
    private async Task<IReadOnlyList<DependanceArete>> ChargerDependancesAsync(string date, CancellationToken ct)
    {
        const string sql = """
            SELECT RubriqueId, DependDeId
            FROM RubriqueDependances
            WHERE DateEffet <= @date AND (DateFin IS NULL OR DateFin >= @date)
            ORDER BY RubriqueId, DependDeId;
            """;
        var rows = await _connection.QueryAsync<DependanceRow>(
            new CommandDefinition(sql, new { date }, cancellationToken: ct));
        return rows.Select(r => new DependanceArete(r.RubriqueId, r.DependDeId)).ToList();
    }

    private async Task<Result<IrgReglePeriode?>> ChargerRegleIrgAsync(string date, CancellationToken ct)
    {
        const string sqlRegle = """
            SELECT Code, BaremeId, ExonerationSeuil, AbattementTaux, AbattementMin, AbattementMax,
                   CoefGeneral, ConstGeneral, CoefSpecial, ConstSpecial, PlafondSpecial
            FROM IRGReglesPeriode
            WHERE DateDebut <= @date AND (DateFin IS NULL OR DateFin >= @date)
            ORDER BY DateDebut DESC LIMIT 1;
            """;
        var regle = await _connection.QuerySingleOrDefaultAsync<IrgRegleRow>(
            new CommandDefinition(sqlRegle, new { date }, cancellationToken: ct));
        if (regle is null)
            return Result.Success<IrgReglePeriode?>(null);

        const string sqlTranches = """
            SELECT BorneInf, BorneSup, Taux FROM BaremeIRGTranches
            WHERE BaremeId = @bid ORDER BY Ordre;
            """;
        var tranches = (await _connection.QueryAsync<IrgTrancheRow>(
            new CommandDefinition(sqlTranches, new { bid = regle.BaremeId }, cancellationToken: ct))).ToList();

        var coefG = Fraction.Parser(regle.CoefGeneral);
        var constG = Fraction.Parser(regle.ConstGeneral);
        var coefS = Fraction.Parser(regle.CoefSpecial);
        var constS = Fraction.Parser(regle.ConstSpecial);
        if (coefG.IsFailure || constG.IsFailure || coefS.IsFailure || constS.IsFailure)
            return Result.Failure<IrgReglePeriode?>(
                Error.Validation($"Fraction IRG invalide pour la période « {regle.Code} »."));

        var regleDomaine = new IrgReglePeriode(
            regle.Code, regle.ExonerationSeuil, (decimal)regle.AbattementTaux,
            regle.AbattementMin, regle.AbattementMax,
            coefG.Value, constG.Value, coefS.Value, constS.Value, regle.PlafondSpecial,
            tranches.Select(t => new IrgTranche(t.BorneInf, t.BorneSup, (decimal)t.Taux)).ToList());
        return Result.Success<IrgReglePeriode?>(regleDomaine);
    }

    // ---- Rows ----

    private sealed record RubriqueRow(string Id, string Nature, long EstImposable, long EstCotisable, long OrdreCalcul, string Expression);
    private sealed record BaremeRow(string Id, string RubriqueId, string Dimension, string BorneInf, string? BorneSup, string TypeValeur, string Valeur, string DateEffet, string? DateFin);
    private sealed record ConditionRow(string Id, string RubriqueId, string CritereId, string? GroupeId, string Operateur, string Valeur, string DateEffet, string? DateFin);
    private sealed record CritereRow(string Id, string Libelle, string TypeValeur, string SourceResolution);
    private sealed record CotisationRow(string Code, string AssietteRef, double? Taux, string TypeCotisation);
    private sealed record DependanceRow(string RubriqueId, string DependDeId);
    private sealed record IrgRegleRow(string Code, string BaremeId, long ExonerationSeuil, double AbattementTaux, long AbattementMin, long AbattementMax, string CoefGeneral, string ConstGeneral, string CoefSpecial, string ConstSpecial, long PlafondSpecial);
    private sealed record IrgTrancheRow(long BorneInf, long? BorneSup, double Taux);

    // ---- Parseurs enum ----

    private static BaremeDimension ParseDimension(string s) => BaremeDimensionKeys.Parser(s);

    private static BaremeTypeValeur ParseTypeValeurBareme(string s) => s switch
    {
        "TAUX" => BaremeTypeValeur.Taux,
        "MONTANT" => BaremeTypeValeur.Montant,
        _ => throw new InvalidOperationException($"Type de valeur de barème inconnu : {s}")
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
        _ => throw new InvalidOperationException($"Opérateur inconnu : {s}")
    };

    private static TypeValeurCritere ParseTypeValeur(string s) => s switch
    {
        "TEXT" => TypeValeurCritere.Text,
        "INT" => TypeValeurCritere.Int,
        "DATE" => TypeValeurCritere.Date,
        "ENUM" => TypeValeurCritere.Enum,
        _ => throw new InvalidOperationException($"Type de valeur inconnu : {s}")
    };

    private static SourceResolution ParseSourceResolution(string s) => s switch
    {
        "ATTRIBUT_AGENT" => SourceResolution.AttributAgent,
        "ATTRIBUT_GRADE" => SourceResolution.AttributGrade,
        "CARRIERE" => SourceResolution.Carriere,
        "CALCULE" => SourceResolution.Calcule,
        _ => throw new InvalidOperationException($"Source de résolution inconnue : {s}")
    };

    private static ReferenceAssiette ParseAssiette(string s) => s switch
    {
        "ASSIETTE_COTISABLE" => ReferenceAssiette.AssietteCotisable,
        "ASSIETTE_IMPOSABLE" => ReferenceAssiette.AssietteImposable,
        "TRAITEMENT_BASE" => ReferenceAssiette.TraitementBase,
        "TRAITEMENT_BRUT" => ReferenceAssiette.TraitementBrut,
        "MONTANT_FIXE" => ReferenceAssiette.MontantFixe,
        _ => throw new InvalidOperationException($"Référence d'assiette inconnue : {s}")
    };
}
