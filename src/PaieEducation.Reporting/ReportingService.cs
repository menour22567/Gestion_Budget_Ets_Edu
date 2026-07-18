using PaieEducation.Domain.Calcul.Snapshot;

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
/// Tout document est dérivé du snapshot immuable — jamais recalculé.
/// </summary>
public sealed class ReportingService
{
    private readonly IDocumentRenderer _pdf;
    private readonly IDocumentRenderer _excel;

    public ReportingService(BulletinPdfRenderer pdf, BulletinExcelExporter excel)
    {
        _pdf = pdf ?? throw new ArgumentNullException(nameof(pdf));
        _excel = excel ?? throw new ArgumentNullException(nameof(excel));
    }

    /// <summary>
    /// Génère les octets du document dans le format demandé.
    /// </summary>
    public byte[] Generer(BulletinSnapshot snapshot, FormatDocument format)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return format switch
        {
            FormatDocument.Pdf => _pdf.Rendre(snapshot),
            FormatDocument.Excel => _excel.Rendre(snapshot),
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

        var complet = format == FormatDocument.Pdf && !chemin.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? chemin + ".pdf"
            : format == FormatDocument.Excel && !chemin.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                ? chemin + ".xlsx"
                : chemin;

        var octets = Generer(snapshot, format);
        File.WriteAllBytes(complet, octets);
        return complet;
    }
}
