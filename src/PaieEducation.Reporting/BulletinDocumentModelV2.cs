using PaieEducation.Domain.Calcul.Snapshot;
using PaieEducation.Reporting.Documents;

namespace PaieEducation.Reporting;

/// <summary>
/// Modèle V2 du bulletin de paie (Phase 7, 7.2b). Identifiant logique
/// <c>"bulletin"</c>, version <c>2</c>. Reprend <see cref="BulletinDocumentModelV1"/>
/// et ajoute :
/// <list type="bullet">
///   <item>Affichage du <see cref="BulletinAffichage.BulletinId"/>.</item>
///   <item>Période lisible (« Juin 2025 » au lieu de « 2025-06-01 »).</item>
///   <item>Section « Cumuls depuis le 1er janvier » (si fournis).</item>
///   <item>Mentions réglementaires algériennes en pied de page.</item>
/// </list>
/// Délègue le rendu PDF au <see cref="BulletinPdfRenderer"/> sous-jacent ;
/// coexiste avec V1 dans le <see cref="DocumentModelRegistry"/>.
/// </summary>
public sealed class BulletinDocumentModelV2 : IDocumentModel<BulletinAffichage>
{
    private readonly BulletinPdfRenderer _pdfRenderer;

    public BulletinDocumentModelV2(BulletinPdfRenderer pdfRenderer)
    {
        _pdfRenderer = pdfRenderer ?? throw new ArgumentNullException(nameof(pdfRenderer));
    }

    public string Id => "bulletin";

    public int Version => 2;

    public byte[] Render(BulletinAffichage input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return _pdfRenderer.Rendre(input, input.Rappels);
    }
}
