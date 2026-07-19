using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Reporting.Documents;

namespace PaieEducation.Reporting;

/// <summary>
/// Modèle V1 du bulletin de paie (Phase 7, 7.1 + 7.2a). Identifiant logique
/// <c>"bulletin"</c>, version <c>1</c>. Délègue le rendu PDF au
/// <see cref="BulletinPdfRenderer"/> sous-jacent ; expose la même surface
/// <see cref="IDocumentModel{TInput}"/> que les futurs modèles (attestations,
/// états récapitulatifs, ordre de virement) utiliseront.
/// </summary>
/// <remarks>
/// Cette indirection (modèle → renderer) permet :
/// (1) d'identifier le modèle dans le <see cref="DocumentModelRegistry"/>
///     par (<c>BulletinSnapshot</c>, "bulletin", 1) ;
/// (2) d'ajouter plus tard une <c>BulletinDocumentModelV2</c> sans casser
///     le contrat d'appel du <c>ReportingService</c>.
/// </remarks>
public sealed class BulletinDocumentModelV1 : IDocumentModel<BulletinSnapshot>
{
    private readonly BulletinPdfRenderer _pdfRenderer;

    public BulletinDocumentModelV1(BulletinPdfRenderer pdfRenderer)
    {
        _pdfRenderer = pdfRenderer ?? throw new ArgumentNullException(nameof(pdfRenderer));
    }

    public string Id => "bulletin";

    public int Version => 1;

    public byte[] Render(BulletinSnapshot input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return _pdfRenderer.Rendre(input);
    }
}
