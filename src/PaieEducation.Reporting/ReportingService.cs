using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Reporting.Documents;

namespace PaieEducation.Reporting;

/// <summary>
/// Format de document de sortie supporté par le module Reporting (C3).
/// </summary>
public enum FormatDocument
{
    Pdf,
    Excel,
}

/// <summary>
/// Service d'orchestration du Reporting : génère un document (PDF/Excel) à
/// partir d'un <see cref="BulletinSnapshot"/> figé, et le persiste sur disque.
/// </summary>
/// <remarks>
/// Phase 7, 7.1+7.2b : la résolution du PDF passe par le
/// <see cref="DocumentModelRegistry"/>. Le modèle V1 (« bulletin », v1) rend
/// un PDF « snapshot seul » ; le modèle V2 (« bulletin », v2) rend le PDF
/// complet (BulletinId, période FR, cumuls, mentions). V2 est le chemin
/// par défaut — V1 reste pour les tests et les exports unitaires.
/// L'Excel utilise pour l'instant <see cref="BulletinExcelExporter"/>
/// directement (sera réintroduit comme modèle dédié en lot 7.4). Tout
/// document PDF est dérivé du snapshot immuable — jamais recalculé.
/// </remarks>
public sealed class ReportingService
{
    /// <summary>Identifiant logique du bulletin dans le registre.</summary>
    public const string BulletinModelId = "bulletin";

    /// <summary>Version par défaut du modèle bulletin (V2 depuis 7.2b).</summary>
    public const int BulletinModelVersion = 2;

    private readonly DocumentModelRegistry _models;
    private readonly BulletinExcelExporter _excel;

    public ReportingService(DocumentModelRegistry models, BulletinExcelExporter excel)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _excel = excel ?? throw new ArgumentNullException(nameof(excel));
    }

    /// <summary>
    /// Génère les octets du document dans le format demandé, à partir du
    /// <see cref="BulletinSnapshot"/> seul (chemin V1, rétrocompat).
    /// </summary>
    public byte[] Generer(BulletinSnapshot snapshot, FormatDocument format)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return format switch
        {
            FormatDocument.Pdf => _models
                .Resolve<BulletinSnapshot>(BulletinModelId, 1)
                .Render(snapshot),
            FormatDocument.Excel => _excel.Rendre(snapshot),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
    }

    /// <summary>
    /// Génère les octets du document à partir d'un
    /// <see cref="BulletinAffichage"/> (chemin V2, 7.2b : BulletinId,
    /// période FR, cumuls, mentions). C'est le chemin utilisé par
    /// <c>ExporterBulletin</c> en production.
    /// </summary>
    public byte[] GenererAffichage(BulletinAffichage affichage, FormatDocument format)
    {
        ArgumentNullException.ThrowIfNull(affichage);
        return format switch
        {
            FormatDocument.Pdf => _models
                .Resolve<BulletinAffichage>(BulletinModelId, BulletinModelVersion)
                .Render(affichage),
            FormatDocument.Excel => _excel.Rendre(affichage),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
    }

    /// <summary>
    /// Génère et enregistre le document à <paramref name="chemin"/>. L'extension
    /// est déduite du format si le chemin n'en porte pas.
    /// </summary>
    public string GenererEtEnregistrer(BulletinSnapshot snapshot, FormatDocument format, string chemin)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(chemin);

        var complet = ResoudreChemin(format, chemin);

        var octets = Generer(snapshot, format);
        File.WriteAllBytes(complet, octets);
        return complet;
    }

    /// <summary>
    /// Variante V2 (<see cref="BulletinAffichage"/>) de
    /// <see cref="GenererEtEnregistrer(BulletinSnapshot, FormatDocument, string)"/>.
    /// </summary>
    public string GenererAffichageEtEnregistrer(BulletinAffichage affichage, FormatDocument format, string chemin)
    {
        ArgumentNullException.ThrowIfNull(affichage);
        ArgumentException.ThrowIfNullOrWhiteSpace(chemin);

        var complet = ResoudreChemin(format, chemin);

        var octets = GenererAffichage(affichage, format);
        File.WriteAllBytes(complet, octets);
        return complet;
    }

    private static string ResoudreChemin(FormatDocument format, string chemin) =>
        format == FormatDocument.Pdf && !chemin.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? chemin + ".pdf"
            : format == FormatDocument.Excel && !chemin.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                ? chemin + ".xlsx"
                : chemin;
}
