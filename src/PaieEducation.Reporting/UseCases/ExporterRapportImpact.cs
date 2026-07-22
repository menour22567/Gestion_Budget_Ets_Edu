using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Reporting.Documents;
using PaieEducation.Shared.Guards;
using PaieEducation.Shared.Results;

namespace PaieEducation.Reporting.UseCases;

/// <inheritdoc />
public sealed class ExporterRapportImpact : IExporterRapportImpact
{
    /// <summary>Identifiant logique du modèle rapport-impact dans le registre.</summary>
    public const string ModelId = "rapport-impact";

    /// <summary>Version par défaut du modèle rapport-impact (V1 en P11).</summary>
    public const int ModelVersion = 1;

    private readonly DocumentModelRegistry _models;
    private readonly ReportingService _reporting;

    public ExporterRapportImpact(DocumentModelRegistry models, ReportingService reporting)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _reporting = reporting ?? throw new ArgumentNullException(nameof(reporting));
    }

    public async Task<Result<string>> ExecuterAsync(IExporterRapportImpact.Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        Guard.AgainstNullOrWhiteSpace(demande.Chemin);
        ArgumentNullException.ThrowIfNull(demande.Document);
        ArgumentNullException.ThrowIfNull(demande.Document.Rapport);

        try
        {
            // Le use case n'a pas de travail asynchrone I/O-bound (pas de base,
            // pas de réseau) — toute la chaîne est CPU. On respecte néanmoins la
            // signature async + CancellationToken pour rester homogène avec
            // ExporterBulletin et permettre un éventuel yield en V2 (export
            // distant, chunked I/O, etc.).
            await Task.CompletedTask;
            ct.ThrowIfCancellationRequested();

            var complet = ResoudreChemin(demande.Chemin);
            var octets = _reporting.GenererRapportImpact(demande.Document, FormatDocument.Pdf);
            File.WriteAllBytes(complet, octets);
            return Result.Success(complet);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Failure<string>(Error.Failure($"Échec de l'export du rapport d'impact : {ex.Message}"));
        }
    }

    private static string ResoudreChemin(string chemin) =>
        !chemin.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? chemin + ".pdf" : chemin;
}
