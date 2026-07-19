using System.Globalization;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Reporting;

namespace PaieEducation.Reporting.UseCases;

/// <summary>
/// Use case C3 : exporte le bulletin déjà validé (snapshot immuable) au format
/// PDF ou Excel, et l'enregistre sur disque. Le document est dérivé du snapshot
/// figé — jamais recalculé (ADR-0008).
/// </summary>
/// <remarks>
/// Phase 7, 7.2b : passe par le chemin V2 (<see cref="BulletinAffichage"/>) —
/// le PDF embarque le <c>BulletinId</c>, la période lisible, les cumuls
/// annuels et les mentions réglementaires. Le BulletinId est lu via
/// <see cref="IBulletinReadRepository.ConsulterAvecBulletinIdAsync"/> ; les
/// cumuls sont agrégés depuis les bulletins validés de l'année civile
/// (<see cref="IBulletinReadRepository.ListerPourPeriodeAsync"/>).
/// </remarks>
public interface IExporterBulletin
{
    Task<Result<string>> ExecuterAsync(Demande demande, CancellationToken ct = default);
}

/// <summary>Demande d'export d'un bulletin validé.</summary>
public sealed record Demande(
    string AgentId,
    string DatePaie,
    FormatDocument Format,
    string Chemin);

/// <inheritdoc />
public sealed class ExporterBulletin : IExporterBulletin
{
    private readonly IBulletinReadRepository _bulletins;
    private readonly ReportingService _reporting;

    public ExporterBulletin(IBulletinReadRepository bulletins, ReportingService reporting)
    {
        _bulletins = bulletins ?? throw new ArgumentNullException(nameof(bulletins));
        _reporting = reporting ?? throw new ArgumentNullException(nameof(reporting));
    }

    public async Task<Result<string>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);

        // 1) Snapshot + BulletinId en une seule requête (V2).
        var lu = await _bulletins.ConsulterAvecBulletinIdAsync(demande.AgentId, demande.DatePaie, ct);
        if (lu.IsFailure)
            return Result.Failure<string>(lu.Error);
        var (snapshot, bulletinId) = lu.Value;

        // 2) Cumuls annuels sur l'année civile du bulletin.
        var annee = ExtraireAnnee(snapshot.Input.DatePaie);
        var liste = await _bulletins.ListerPourPeriodeAsync(
            demande.AgentId, $"{annee:D4}-01-01", $"{annee:D4}-12-31", ct);
        if (liste.IsFailure)
            return Result.Failure<string>(liste.Error);
        var bulletins = liste.Value.Select(s => s.Resultat).ToList();
        var cumuls = CumulsAnnuels.FromBulletins(annee, bulletins);

        var affichage = new BulletinAffichage(snapshot, bulletinId, cumuls);

        try
        {
            var chemin = _reporting.GenererAffichageEtEnregistrer(affichage, demande.Format, demande.Chemin);
            return Result.Success(chemin);
        }
        catch (Exception ex)
        {
            return Result.Failure<string>(Error.Failure($"Échec de l'export : {ex.Message}"));
        }
    }

    private static int ExtraireAnnee(string datePaie)
    {
        if (DateTime.TryParseExact(datePaie, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt))
        {
            return dt.Year;
        }
        return DateTime.UtcNow.Year;
    }
}
