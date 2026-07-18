using ClosedXML.Excel;
using PaieEducation.Domain.Calcul.Snapshot;

namespace PaieEducation.Reporting;

/// <summary>
/// Export Excel d'un bulletin de paie à partir du <see cref="BulletinSnapshot"/>
/// immuable (C3.3). Utilise ClosedXML. Le rendu est déterministe : aucune
/// relecture de base, aucun recalcul.
/// </summary>
public sealed class BulletinExcelExporter : IDocumentRenderer
{
    public byte[] Rendre(BulletinSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Bulletin");

        var agent = snapshot.Input.Agent;
        var bulletin = snapshot.Resultat;
        var culture = System.Globalization.CultureInfo.GetCultureInfo("fr-DZ");

        // En-tête
        worksheet.Cell(1, 1).Value = "RÉPUBLIQUE ALGÉRIENNE DÉMOCRATIQUE ET POPULAIRE";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 12;
        worksheet.Cell(1, 4).Value = "Ministère de l'Éducation Nationale";
        worksheet.Cell(1, 4).Style.Font.FontSize = 10;

        worksheet.Cell(2, 1).Value = "BULLETIN DE PAIE";
        worksheet.Cell(2, 1).Style.Font.Bold = true;
        worksheet.Cell(2, 1).Style.Font.FontSize = 16;
        worksheet.Cell(2, 1).Style.Font.FontColor = XLColor.Blue;

        // Infos agent
        var row = 4;
        worksheet.Cell(row, 1).Value = "Agent";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 2).Value = agent.Grade ?? agent.Corps ?? "—";

        row++;
        worksheet.Cell(row, 1).Value = "Catégorie";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 2).Value = agent.Categorie?.ToString(culture) ?? "—";

        row++;
        worksheet.Cell(row, 1).Value = "Échelon";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 2).Value = agent.Echelon?.ToString(culture) ?? "—";

        row++;
        worksheet.Cell(row, 1).Value = "Date de paie";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 2).Value = snapshot.Input.DatePaie;

        // Détail du bulletin
        row += 2;
        worksheet.Cell(row, 1).Value = "Détail du bulletin";
        worksheet.Cell(row, 1).Style.Font.Bold = true;

        row++;
        var headers = new[] { "Rubrique", "Imposable", "Cotisable", "Montant (DA)" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        foreach (var ligne in bulletin.Lignes)
        {
            row++;
            worksheet.Cell(row, 1).Value = ligne.RubriqueId;
            worksheet.Cell(row, 2).Value = ligne.Imposable ? "Oui" : "Non";
            worksheet.Cell(row, 3).Value = ligne.Cotisable ? "Oui" : "Non";
            worksheet.Cell(row, 4).Value = ligne.Montant.Amount.ToString("N2", culture);
            worksheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            for (int i = 1; i <= 4; i++)
            {
                worksheet.Cell(row, i).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
        }

        // Totaux
        var totaux = new (string Label, decimal Montant)[]
        {
            ("Total gains", bulletin.TotalGains.Amount),
            ("Assiette cotisable", bulletin.AssietteCotisable.Amount),
            ("Assiette imposable", bulletin.AssietteImposable.Amount),
            ("Total retenues", bulletin.TotalRetenues.Amount),
            ("IRG", bulletin.Irg.Amount),
        };

        foreach (var t in totaux)
        {
            row++;
            worksheet.Cell(row, 1).Value = t.Label;
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 4).Value = t.Montant.ToString("N2", culture);
            worksheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            for (int i = 1; i <= 4; i++)
            {
                worksheet.Cell(row, i).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
        }

        // Net à payer
        row++;
        worksheet.Cell(row, 1).Value = "NET À PAYER";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 1).Style.Font.FontSize = 14;
        worksheet.Cell(row, 1).Style.Font.FontColor = XLColor.DarkGreen;
        worksheet.Cell(row, 4).Value = bulletin.Net.Amount.ToString("N2", culture);
        worksheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
        worksheet.Cell(row, 4).Style.Font.Bold = true;
        worksheet.Cell(row, 4).Style.Font.FontSize = 14;
        worksheet.Cell(row, 4).Style.Font.FontColor = XLColor.DarkGreen;
        for (int i = 1; i <= 4; i++)
        {
            worksheet.Cell(row, i).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // Pied
        row += 2;
        worksheet.Cell(row, 1).Value = $"Snapshot capturé le {snapshot.CapturesLe}";
        worksheet.Cell(row, 1).Style.Font.FontSize = 8;
        worksheet.Cell(row, 1).Style.Font.FontColor = XLColor.Gray;

        worksheet.Cell(row, 4).Value = "Document généré automatiquement — ne pas modifier";
        worksheet.Cell(row, 4).Style.Font.FontSize = 8;
        worksheet.Cell(row, 4).Style.Font.FontColor = XLColor.Gray;
        worksheet.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        // Ajuster les largeurs
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}