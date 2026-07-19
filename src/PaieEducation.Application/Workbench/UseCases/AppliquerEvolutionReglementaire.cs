using System.Text.Json;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Common;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Domain.Workbench.Constants;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Stratégie de versionning appliquée (J3I §7.4). Seuls les 2 modes avec un
/// chemin d'écriture existant sont couverts — « Modification en place »
/// (rare, sans précédent de code) reste hors périmètre.
/// </summary>
public enum StrategieVersionning
{
    /// <summary>Clôture la version en vigueur, insère une nouvelle valeur (<c>DefinirValeurPointAsync</c>).</summary>
    ClotureEtNouvelleVersion,

    /// <summary>Clone la valeur en vigueur vers une nouvelle période (<c>DupliquerValeurPointAsync</c>).</summary>
    Duplication,
}

/// <summary>
/// Commit d'une évolution réglementaire du point indiciaire (D8) — le
/// dry-run → validation → commit → audit décrit par
/// <c>docs/PLAN_ACTION.md</c> Phase 5 §5. Portée limitée à <c>ValeurPoint</c>
/// (même périmètre que <c>GérerRéférentiels</c>/<c>DupliquerVersion</c>).
/// </summary>
/// <remarks>
/// <b>Correction du 19/07/2026 (D3, audit d'avancement) :</b> l'écriture
/// réglementaire et la ligne <c>AuditLog</c> **sont** atomiques entre elles —
/// ce use case injecte <see cref="IUnitOfWork"/> et encadre l'écriture et
/// l'enregistrement d'audit dans une transaction unique
/// (<c>BeginAsync</c>/<c>CommitAsync</c>/<c>RollbackAsync</c>, voir
/// <see cref="ExecuterAsync"/>) : si l'audit échoue après une écriture
/// réglementaire réussie, la transaction est annulée dans son ensemble — le
/// changement réglementaire ne reste jamais en base sans sa ligne d'audit.
/// (Le commentaire précédent, affirmant l'absence d'<c>IUnitOfWork</c> et une
/// incohérence possible, était obsolète depuis l'introduction de cette
/// transaction ; corrigé ici sans changement de comportement.)
/// <see cref="Demande.RapportImpact"/>
/// exige que l'appelant ait déjà obtenu un rapport de
/// <c>SimulerEvolutionReglementaire</c> — une convention de forme d'API, pas
/// une preuve cryptographique du dry-run — <b>sauf bypass admin explicite</b>
/// (Phase 5, tâche 6 : « rejet de tout commit Workbench sans dry-run
/// préalable, sauf bypass admin documenté ») : <see cref="Demande.BypassDryRun"/>
/// permet de committer sans <see cref="Demande.RapportImpact"/>, à condition
/// de fournir <see cref="Demande.RaisonBypass"/> — tracé avec une <c>Action</c>
/// distincte dans <c>AuditLog</c> (<c>APPLIQUER_EVOLUTION_BYPASS</c>),
/// filtrable sans désérialiser le <c>Payload</c>.
/// </remarks>
public sealed class AppliquerEvolutionReglementaire
{
    public sealed record Demande(
        string Description,
        RapportImpact? RapportImpact,
        StrategieVersionning Strategie,
        decimal? NouvelleValeur,
        string DateEffet,
        string Version,
        string? Source,
        string Actor,
        bool BypassDryRun = false,
        string? RaisonBypass = null);

    private sealed record AuditPayload(
        string Description,
        string Strategie,
        string DateEffet,
        string Version,
        string? Source,
        decimal? NouvelleValeur,
        RapportImpact? RapportImpact,
        bool BypassDryRun,
        string? RaisonBypass);

    private readonly IGrilleIndiciaireRepository _grille;
    private readonly IAuditLogRepository _auditLog;
    private readonly IClock _clock;
    private readonly IUnitOfWork _uow;

    public AppliquerEvolutionReglementaire(IGrilleIndiciaireRepository grille, IAuditLogRepository auditLog, IClock clock, IUnitOfWork uow)
    {
        _grille = grille ?? throw new ArgumentNullException(nameof(grille));
        _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    public async Task<Result<string>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        Guard.AgainstNullOrWhiteSpace(demande.Description);
        Guard.AgainstNullOrWhiteSpace(demande.DateEffet);
        Guard.AgainstNullOrWhiteSpace(demande.Version);
        Guard.AgainstNullOrWhiteSpace(demande.Actor);

        if (demande.Strategie == StrategieVersionning.ClotureEtNouvelleVersion && demande.NouvelleValeur is null)
            return Result.Failure<string>(Error.Validation(
                "La stratégie « clôture + nouvelle version » exige une nouvelle valeur du point indiciaire."));

        if (demande.BypassDryRun)
        {
            if (string.IsNullOrWhiteSpace(demande.RaisonBypass))
                return Result.Failure<string>(Error.Validation(
                    "Un bypass admin exige une raison documentée (tracée dans AuditLog)."));
        }
        else if (demande.RapportImpact is null)
        {
            return Result.Failure<string>(Error.Validation(
                "Le dry-run est obligatoire (RapportImpact requis) — ou passer BypassDryRun avec une raison documentée."));
        }

        await _uow.BeginAsync(ct);

        var ecriture = demande.Strategie == StrategieVersionning.ClotureEtNouvelleVersion
            ? await _grille.DefinirValeurPointAsync(
                demande.NouvelleValeur!.Value, demande.DateEffet, demande.Version, demande.Source, _clock.UtcNow, ct, _uow)
            : await _grille.DupliquerValeurPointAsync(demande.DateEffet, demande.Version, demande.Source, _clock.UtcNow, ct, _uow);

        if (ecriture.IsFailure)
        {
            await _uow.RollbackAsync(ct);
            return ecriture;
        }

        var payload = JsonSerializer.Serialize(new AuditPayload(
            demande.Description, demande.Strategie.ToString(), demande.DateEffet, demande.Version, demande.Source,
            demande.NouvelleValeur, demande.RapportImpact, demande.BypassDryRun, demande.RaisonBypass));

        var action = demande.BypassDryRun ? AuditActions.AppliquerEvolutionBypass : AuditActions.AppliquerEvolution;
        var audit = await _auditLog.EnregistrerAsync(
            demande.Actor, action, AuditEntityTypes.ValeurPoint, ecriture.Value, payload, demande.Description, _clock.UtcNow, ct, _uow);

        if (audit.IsFailure)
        {
            await _uow.RollbackAsync(ct);
            return Result.Failure<string>(audit.Error);
        }

        await _uow.CommitAsync(ct);
        return ecriture;
    }
}
