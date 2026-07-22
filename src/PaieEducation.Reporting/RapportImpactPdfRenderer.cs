using System.Globalization;
using PaieEducation.Application.Workbench.UseCases;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PaieEducation.Reporting;

/// <summary>
/// Rendu PDF d'un <see cref="RapportImpactDocument"/> (chantier P11, audit
/// du 19/07/2026). Document **d'archivage et de validation hiérarchique**
/// (D8 — toute évolution réglementaire validée est accompagnée d'un rapport
/// consultable et signé).
/// </summary>
/// <remarks>
/// Le rendu est strictement déterministe : il ne consulte aucune base, ne
/// refait aucun calcul — les 6 champs du <see cref="RapportImpact"/> sous-jacent
/// sont la source de vérité, l'enveloppe n'apporte que des métadonnées
/// documentaires (hypothèse, horodatage, erreurs).
/// </remarks>
public sealed class RapportImpactPdfRenderer
{
    private static readonly CultureInfo _culture = CultureInfo.GetCultureInfo("fr-DZ");

    static RapportImpactPdfRenderer()
    {
        // Même licence Community que le BulletinPdfRenderer (usage hors
        // production, monolithe desktop autonome).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Rendre(RapportImpactDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Rapport);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Calibri));
                page.PageColor(Colors.White);

                page.Header().Element(c => BuildHeader(c, document));
                page.Content().Element(c => BuildBody(c, document));
                page.Footer().Element(c => BuildFooter(c, document));
            });
        }).GeneratePdf();
    }

    // ----- Mise en forme -----

    private static string Dzd(decimal montant) =>
        montant.ToString("N2", _culture) + " DA";

    private static string DzdSigne(decimal montant) =>
        (montant < 0 ? "- " : "+ ") + Math.Abs(montant).ToString("N2", _culture) + " DA";

    // ----- Sections -----

    private static void BuildHeader(IContainer container, RapportImpactDocument document)
    {
        container.Column(col =>
        {
            col.Item().Text("RAPPORT D'IMPACT D'ÉVOLUTION RÉGLEMENTAIRE")
                .FontSize(14).SemiBold().FontColor(Colors.Blue.Medium);
            col.Item().Text($"Dry-run du {document.Horodatage.ToString("F", _culture)}")
                .FontSize(9).Italic().FontColor(Colors.Grey.Darken1);
            col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

            if (!string.IsNullOrWhiteSpace(document.Hypothese))
            {
                col.Item().PaddingTop(5).Text("Hypothèse d'évolution").SemiBold().FontSize(10);
                col.Item().Text(document.Hypothese).FontSize(10);
            }
        });
    }

    private static void BuildBody(IContainer container, RapportImpactDocument document)
    {
        var r = document.Rapport;
        container.PaddingVertical(10).Column(col =>
        {
            // ----- Métriques de l'impact -----
            col.Item().Text("Impact mesuré").SemiBold().FontSize(11);
            col.Item().PaddingTop(5).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(2);
                });
                Ligne(table, "Nombre d'agents impactés", r.NbAgents.ToString(_culture));
                Ligne(table, "Delta mensuel minimum", DzdSigne(r.DeltaMinMensuel));
                Ligne(table, "Delta mensuel maximum", DzdSigne(r.DeltaMaxMensuel));
                Ligne(table, "Montant total mensuel", Dzd(r.MontantTotalMensuel));
                Ligne(table, "Période impactée", r.PeriodeImpactee);
                Ligne(table, "Bulletins validés concernés (rappels)", r.BulletinsAvertis.ToString(_culture));
            });

            // ----- Bandeau d'erreurs/avertissements -----
            if (document.Erreurs.Count > 0)
            {
                col.Item().PaddingTop(15).Text("Avertissements").SemiBold().FontSize(11)
                    .FontColor(Colors.Orange.Darken2);
                col.Item().PaddingTop(5).Background(Colors.Orange.Lighten4).Padding(5).Column(errCol =>
                {
                    foreach (var err in document.Erreurs)
                    {
                        errCol.Item().Text("• " + err).FontSize(9);
                    }
                });
            }

            // ----- Mention d'archivage -----
            col.Item().PaddingTop(20).Text(
                "Ce rapport est généré à des fins d'archivage et de validation hiérarchique. "
              + "Toute évolution réglementaire validée dans l'application doit être accompagnée "
              + "de ce document (D8 — auditabilité). Le rapport est strictement informatif : "
              + "il n'autorise ni ne déclenche aucun commit par lui-même.")
                .FontSize(8).Italic().FontColor(Colors.Grey.Darken2);
        });
    }

    private static void BuildFooter(IContainer container, RapportImpactDocument document)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
            col.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem().Text(t =>
                {
                    t.Span("PaieEducation v0-pilote-moteur — ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.Span($"Rapport d'impact généré le {document.Horodatage.ToString("F", _culture)}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
                row.RelativeItem().AlignRight().Text(t =>
                {
                    t.Span("Page ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.Span(" / ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });
    }

    private static void Ligne(QuestPDF.Fluent.TableDescriptor table, string libelle, string valeur)
    {
        table.Cell().PaddingVertical(2).Text(libelle).FontSize(10);
        table.Cell().PaddingVertical(2).AlignRight().Text(valeur).FontSize(10).SemiBold();
    }
}
