using PaieEducation.Shared.Results;

namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Port (interface) d'export d'un <see cref="RapportImpactDocument"/> en
/// fichier PDF sur disque. Vit dans <c>Application</c> car consommé par
/// <c>AppliquerEvolutionReglementaire</c> qui ne peut pas référencer
/// <c>Reporting</c> (Clean Architecture — <c>Application</c> ne dépend que
/// de <c>Domain</c>, jamais de <c>Reporting</c>).
/// </summary>
/// <remarks>
/// L'implémentation (<c>Reporting/UseCases/ExporterRapportImpact.cs</c>)
/// utilise le <c>DocumentModelRegistry</c> pour résoudre le modèle
/// versionné et le <c>ReportingService</c> pour générer les octets, puis
/// écrit le fichier au chemin fourni. Le format **PDF est le seul supporté
/// en V1** (P11) — l'export Excel d'un rapport d'impact n'est pas défini
/// ; le port est figé sur PDF par simplification.
/// </remarks>
public interface IExporterRapportImpact
{
    Task<Result<string>> ExecuterAsync(Demande demande, CancellationToken ct = default);

    /// <summary>
    /// Demande d'export d'un rapport d'impact en PDF.
    /// <paramref name="Chemin"/> : si l'extension <c>.pdf</c> manque, elle
    /// est ajoutée par l'implémentation.
    /// </summary>
    sealed record Demande(
        RapportImpactDocument Document,
        string Chemin);
}
