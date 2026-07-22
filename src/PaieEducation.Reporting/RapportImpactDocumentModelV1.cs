using PaieEducation.Application.Workbench.UseCases;
using PaieEducation.Reporting.Documents;

namespace PaieEducation.Reporting;

/// <summary>
/// Modèle V1 du rapport d'impact d'évolution réglementaire (chantier P11).
/// Identifiant logique <c>"rapport-impact"</c>, version <c>1</c>. Délègue
/// le rendu PDF au <see cref="RapportImpactPdfRenderer"/> sous-jacent ;
/// coexiste avec les modèles bulletin dans le <see cref="DocumentModelRegistry"/>.
/// </summary>
/// <remarks>
/// Mêmes invariants que <see cref="BulletinDocumentModelV1"/> : rendu strict,
/// déterministe, sans I/O ; aucune dépendance métier (le rapport est
/// l'enveloppe déjà calculée par <c>SimulerEvolutionReglementaire</c>).
/// </remarks>
public sealed class RapportImpactDocumentModelV1 : IDocumentModel<RapportImpactDocument>
{
    private readonly RapportImpactPdfRenderer _pdfRenderer;

    public RapportImpactDocumentModelV1(RapportImpactPdfRenderer pdfRenderer)
    {
        _pdfRenderer = pdfRenderer ?? throw new ArgumentNullException(nameof(pdfRenderer));
    }

    public string Id => "rapport-impact";

    public int Version => 1;

    public byte[] Render(RapportImpactDocument input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return _pdfRenderer.Rendre(input);
    }
}
