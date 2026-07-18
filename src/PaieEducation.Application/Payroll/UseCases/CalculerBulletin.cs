using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Application.Payroll.Services;

namespace PaieEducation.Application.Payroll.UseCases;

/// <summary>
/// Use case pilote (Phase 5, tâche 4) : calcule le bulletin d'un agent à une
/// date de paie donnée. Lecture seule — n'écrit rien en base (la persistance
/// du bulletin validé relève d'un use case distinct, <c>ValiderBulletin</c>).
/// </summary>
/// <remarks>
/// Orchestre les ports du sous-arbre Calcul, puis <see cref="CalculationPipeline"/>
/// (service Domain pur). Les entrées de calcul (<see cref="Demande.SourcesValeur"/>
/// et <see cref="Demande.ClesBareme"/>) sont <b>auto-résolues</b> depuis le
/// dossier agent (C2.2/C2.3) ; l'appelant peut encore les fournir en surcharge
/// (rétro-compatibilité), elles sont alors fusionnées par-dessus l'auto-resolution.
/// Le <see cref="ModeArrondi"/> est lu depuis <c>Parametres</c> (C2.1), plus codé
/// en dur.
/// </remarks>
public sealed class CalculerBulletin
{
    /// <summary>Demande de calcul d'un bulletin pour un agent à une date de paie.</summary>
    /// <param name="VpiOverride">
    /// Option « what-if » pour la simulation d'évolution réglementaire (D8,
    /// ADR-0007) : surcharge la VPI par une valeur hypothétique sans modifier
    /// la base. <c>null</c> = lecture DB normale (cas par défaut). Cf. J5L
    /// §3.2 — D-S2. Tous les autres paramètres (<c>INDICE_MIN</c>,
    /// <c>INDICE_ECH</c>) restent lus depuis la base.
    /// </param>
    public sealed record Demande(
        string AgentId,
        string DatePaie,
        IReadOnlyDictionary<string, decimal>? SourcesValeur = null,
        IReadOnlyDictionary<string, string>? ClesBareme = null,
        ProfilFiscal Profil = ProfilFiscal.Standard,
        decimal? VpiOverride = null);

    private readonly IAgentCarriereRepository _agents;
    private readonly IVariableRepository _variables;
    private readonly IPayrollReadRepository _payroll;
    private readonly IParametreSystemeRepository _parametres;
    private readonly CalculEntreeResolver _entrees;

    public CalculerBulletin(
        IAgentCarriereRepository agents,
        IVariableRepository variables,
        IPayrollReadRepository payroll,
        IParametreSystemeRepository parametres,
        CalculEntreeResolver entrees)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _variables = variables ?? throw new ArgumentNullException(nameof(variables));
        _payroll = payroll ?? throw new ArgumentNullException(nameof(payroll));
        _parametres = parametres ?? throw new ArgumentNullException(nameof(parametres));
        _entrees = entrees ?? throw new ArgumentNullException(nameof(entrees));
    }

    public async Task<Result<Bulletin>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);

        var calcule = await ResoudreAsync(demande, ct);
        return calcule.IsFailure
            ? Result.Failure<Bulletin>(calcule.Error)
            : Result.Success(calcule.Value.Bulletin);
    }

    /// <summary>
    /// Résout le bulletin complet (input + bulletin) en appliquant l'auto-resolution
    /// des entrées (C2.2/C2.3) et le mode d'arrondi paramétré (C2.1). Partagé par
    /// <see cref="ValiderBulletin"/> et <see cref="GenererRappels"/> pour ne pas
    /// dupliquer l'orchestration de calcul.
    /// </summary>
    public async Task<Result<(PaieEducation.Domain.Calcul.Pipeline.PayrollInput Input, Bulletin Bulletin)>> ResoudreAsync(
        Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);

        var agent = await _agents.ResoudreAsync(demande.AgentId, demande.DatePaie, ct);
        if (agent.IsFailure)
            return Result.Failure<(PaieEducation.Domain.Calcul.Pipeline.PayrollInput, Bulletin)>(agent.Error);

        // D8 / ADR-0007 : variante « what-if » pour la simulation d'évolution
        // réglementaire. VpiOverride != null ⇒ on appelle la variante du
        // repository qui utilise la VPI hypothétique au lieu de la lecture DB.
        // VpiOverride == null ⇒ chemin nominal (lecture DB).
        Result<IReadOnlyDictionary<string, decimal>> variables = demande.VpiOverride is { } vpiSimulee
            ? await _variables.ResoudreAvecVPIAsync(agent.Value, demande.DatePaie, vpiSimulee, ct)
            : await _variables.ResoudreAsync(agent.Value, demande.DatePaie, ct);
        if (variables.IsFailure)
            return Result.Failure<(PaieEducation.Domain.Calcul.Pipeline.PayrollInput, Bulletin)>(variables.Error);

        // C2.2 / C2.3 — auto-résolution depuis le dossier agent, fusionnée avec
        // d'éventuelles surcharges fournies par l'appelant.
        var clesBareme = Fusionner(_entrees.ResoudreClesBareme(agent.Value), demande.ClesBareme);

        // C8.4 — BASE_PAPP et NOTE_MAX_PAPP lus depuis Parametres (obligatoires).
        var basePapp = await _parametres.LireDecimalObligatoireAsync("BASE_PAPP", demande.DatePaie, ct);
        if (basePapp.IsFailure)
            return Result.Failure<(PaieEducation.Domain.Calcul.Pipeline.PayrollInput, Bulletin)>(basePapp.Error);

        var noteMax = await _parametres.LireDecimalObligatoireAsync("NOTE_MAX_PAPP", demande.DatePaie, ct);
        if (noteMax.IsFailure)
            return Result.Failure<(PaieEducation.Domain.Calcul.Pipeline.PayrollInput, Bulletin)>(noteMax.Error);

        var sourcesValeur = Fusionner(
            _entrees.ResoudreSourcesValeur(agent.Value, demande.DatePaie, basePapp.Value, noteMax.Value),
            demande.SourcesValeur);

        var input = await _payroll.ChargerAsync(
            agent.Value, demande.DatePaie, variables.Value, sourcesValeur, clesBareme,
            demande.Profil, ct);
        if (input.IsFailure)
            return Result.Failure<(PaieEducation.Domain.Calcul.Pipeline.PayrollInput, Bulletin)>(input.Error);

        // C2.1 — mode d'arrondi lu depuis Parametres (plus de hardcoding).
        var mode = await _parametres.LireModeArrondiAsync(demande.DatePaie, ct);
        if (mode.IsFailure)
            return Result.Failure<(PaieEducation.Domain.Calcul.Pipeline.PayrollInput, Bulletin)>(mode.Error);

        // C8.1 — seuil exonération et plafond lissage lus depuis Parametres (obligatoires).
        var seuilExoneration = await _parametres.LireDecimalObligatoireAsync("SEUIL_EXONERATION_IRG", demande.DatePaie, ct);
        if (seuilExoneration.IsFailure)
            return Result.Failure<(PaieEducation.Domain.Calcul.Pipeline.PayrollInput, Bulletin)>(seuilExoneration.Error);

        var plafondLissage = await _parametres.LireDecimalObligatoireAsync("PLAFOND_LISSAGE_GENERAL", demande.DatePaie, ct);
        if (plafondLissage.IsFailure)
            return Result.Failure<(PaieEducation.Domain.Calcul.Pipeline.PayrollInput, Bulletin)>(plafondLissage.Error);

        var bulletin = new CalculationPipeline(new ArrondiService(mode.Value), seuilExoneration.Value, plafondLissage.Value).Calculer(input.Value);
        if (bulletin.IsFailure)
            return Result.Failure<(PaieEducation.Domain.Calcul.Pipeline.PayrollInput, Bulletin)>(bulletin.Error);

        return Result.Success((input.Value, bulletin.Value));
    }

    private static Dictionary<string, T> Fusionner<T>(
        IReadOnlyDictionary<string, T> auto, IReadOnlyDictionary<string, T>? surcharge)
    {
        var result = new Dictionary<string, T>(auto, StringComparer.OrdinalIgnoreCase);
        if (surcharge is not null)
            foreach (var kv in surcharge)
                result[kv.Key] = kv.Value;
        return result;
    }
}

