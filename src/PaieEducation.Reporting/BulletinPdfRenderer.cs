using System.Globalization;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Snapshot;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PaieEducation.Reporting;

/// <summary>
/// Rendu PDF d'un bulletin de paie à partir du <see cref="BulletinSnapshot"/>
/// immuable (C3.2). Utilise QuestPDF (référencé mais jusqu'ici non utilisé).
/// Le rendu est déterministe : aucune relecture de base, aucun recalcul.
/// </summary>
public sealed class BulletinPdfRenderer : IDocumentRenderer
{
    private static readonly CultureInfo _culture = CultureInfo.GetCultureInfo("fr-DZ");

    static BulletinPdfRenderer()
    {
        // Licence Community (usage hors production, monolithe desktop autonome).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Rendre(BulletinSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Calibri));
                page.PageColor(Colors.White);

                page.Header().Element(BuilderEnTete(snapshot));
                page.Content().Element(BuilderCorps(snapshot));
                page.Footer().Element(BuilderPied(snapshot));
            });
        }).GeneratePdf();
    }

    private static string Dzd(decimal montant) =>
        montant.ToString("N2", _culture) + " DA";

    private static Action<IContainer> BuilderEnTete(BulletinSnapshot snapshot)
    {
        var agent = snapshot.Input.Agent;
        return c => c.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("RÉPUBLIQUE ALGÉRIENNE DÉMOCRATIQUE ET POPULAIRE").SemiBold().FontSize(10);
                row.RelativeItem().AlignRight().Text("Ministère de l'Éducation Nationale").FontSize(9);
            });
            col.Item().Text("BULLETIN DE PAIE").FontSize(14).SemiBold().FontColor(Colors.Blue.Medium);
            col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            col.Item().Table(grid =>
            {
                grid.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn();
                    cols.RelativeColumn();
                    cols.RelativeColumn();
                    cols.RelativeColumn();
                });
                CelluleTexte(grid, "Agent", agent.Grade ?? agent.Corps ?? "—");
                CelluleTexte(grid, "Catégorie", (agent.Categorie?.ToString(_culture)) ?? "—");
                CelluleTexte(grid, "Échelon", (agent.Echelon?.ToString(_culture)) ?? "—");
                CelluleTexte(grid, "Date de paie", snapshot.Input.DatePaie);
            });
        });
    }

    private static Action<IContainer> BuilderCorps(BulletinSnapshot snapshot)
    {
        return c => c.Column(col =>
        {
            var bulletin = snapshot.Resultat;
            col.Item().Text("Détail du bulletin").SemiBold();
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(4);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                });
                table.Header(header =>
                {
                    header.Cell().Text("Rubrique").SemiBold();
                    header.Cell().AlignRight().Text("Imposable").SemiBold();
                    header.Cell().AlignRight().Text("Cotisable").SemiBold();
                    header.Cell().AlignRight().Text("Montant").SemiBold();
                });
                foreach (var ligne in bulletin.Lignes)
                {
                    table.Cell().Text(ligne.RubriqueId);
                    table.Cell().AlignRight().Text(ligne.Imposable ? "Oui" : "Non");
                    table.Cell().AlignRight().Text(ligne.Cotisable ? "Oui" : "Non");
                    table.Cell().AlignRight().Text(Dzd(ligne.Montant.Amount));
                }
            });

            col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            col.Item().Table(totaux =>
            {
                totaux.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(1);
                });
                LigneTotaux(totaux, "Total gains", bulletin.TotalGains.Amount);
                LigneTotaux(totaux, "Assiette cotisable", bulletin.AssietteCotisable.Amount);
                LigneTotaux(totaux, "Assiette imposable", bulletin.AssietteImposable.Amount);
                LigneTotaux(totaux, "Total retenues", bulletin.TotalRetenues.Amount);
                LigneTotaux(totaux, "IRG", bulletin.Irg.Amount);
                totaux.Cell().Text("NET À PAYER").FontSize(12).SemiBold().FontColor(Colors.Green.Darken2);
                totaux.Cell().AlignRight().Text(Dzd(bulletin.Net.Amount)).FontSize(12).SemiBold();
            });
        });
    }

    private static Action<IContainer> BuilderPied(BulletinSnapshot snapshot)
    {
        return c => c.Row(row =>
        {
            row.RelativeItem().Text($"Snapshot capturé le {snapshot.CapturesLe}").FontSize(7).FontColor(Colors.Grey.Medium);
            row.RelativeItem().AlignRight().Text("Document généré automatiquement — ne pas modifier").FontSize(7).FontColor(Colors.Grey.Medium);
        });
    }

    private static void CelluleTexte(TableDescriptor table, string libelle, string valeur)
    {
        table.Cell().Column(col =>
        {
            col.Item().Text(libelle).FontSize(7).FontColor(Colors.Grey.Medium);
            col.Item().Text(valeur).FontSize(9).SemiBold();
        });
    }

    private static void LigneTotaux(TableDescriptor table, string libelle, decimal montant)
    {
        table.Cell().Text(libelle).SemiBold();
        table.Cell().AlignRight().Text(Dzd(montant));
    }
}
