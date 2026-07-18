using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Reporting;

namespace PaieEducation.Reporting.UseCases;

/// <summary>
/// Use case C3 : exporte le bulletin déjà validé (snapshot immuable) au format
/// PDF ou Excel, et l'enregistre sur disque. Le document est dérivé du snapshot
/// figé — jamais recalculé (ADR-0008). Situé dans le projet Reporting (qui
/// consomme Application), conformément à la matrice de dépendances.
/// </summary>
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

        var snapshot = await _bulletins.ConsulterAsync(demande.AgentId, demande.DatePaie, ct);
        if (snapshot.IsFailure)
            return Result.Failure<string>(snapshot.Error);

        try
        {
            var chemin = _reporting.GenererEtEnregistrer(snapshot.Value, demande.Format, demande.Chemin);
            return Result.Success(chemin);
        }
        catch (Exception ex)
        {
            return Result.Failure<string>(Error.Failure($"Échec de l'export : {ex.Message}"));
        }
    }
}
