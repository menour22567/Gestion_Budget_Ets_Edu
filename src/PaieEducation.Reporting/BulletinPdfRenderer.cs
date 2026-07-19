using System.Globalization;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Rappels;
using PaieEducation.Domain.Calcul.Snapshot;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PaieEducation.Reporting;

/// <summary>
/// Rendu PDF d'un bulletin de paie à partir du <see cref="BulletinSnapshot"/>
/// immuable (C3.2). Utilise QuestPDF. Le rendu est déterministe : aucune
/// relecture de base, aucun recalcul.
/// </summary>
/// <remarks>
/// Sections rendues (Phase 7, 7.2a + 7.2b) :
/// <list type="bullet">
///   <item>En-tête : entête administratif + identification agent/période
///         + BulletinId (V2) + période lisible (« Juin 2025 »).</item>
///   <item>Corps : détail des rubriques + totaux + net à payer + section
///         « Cumuls depuis le 1er janvier » (V2, si <see cref="CumulsAnnuels"/>
///         fourni) + section « Rappels » (D9).</item>
///   <item>Pied : horodatage du snapshot + mentions réglementaires
///         algériennes (V2) + mention d'inviolabilité.</item>
/// </list>
/// </remarks>
public sealed class BulletinPdfRenderer : IDocumentRenderer
{
    private static readonly CultureInfo _culture = CultureInfo.GetCultureInfo("fr-DZ");

    static BulletinPdfRenderer()
    {
        // Licence Community (usage hors production, monolithe desktop autonome).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // ----- Surcharges (V1 et V2) -----

    public byte[] Rendre(BulletinSnapshot snapshot, IReadOnlyList<LigneRappel>? rappels = null)
        => Rendre(BulletinAffichage.FromSnapshot(snapshot), rappels);

    public byte[] Rendre(BulletinAffichage affichage, IReadOnlyList<LigneRappel>? rappels = null)
    {
        ArgumentNullException.ThrowIfNull(affichage);
        ArgumentNullException.ThrowIfNull(affichage.Snapshot);
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Calibri));
                page.PageColor(Colors.White);

                page.Header().Element(BuildHeader(affichage));
                page.Content().Element(BuildBody(affichage, rappels));
                page.Footer().Element(BuildFooter(affichage));
            });
        }).GeneratePdf();
    }

    // ----- Mise en forme -----

    private static string Dzd(decimal montant) =>
        montant.ToString("N2", _culture) + " DA";

    private static string DzdSigne(decimal montant) =>
        (montant < 0 ? "- " : "+ ") + Math.Abs(montant).ToString("N2", _culture) + " DA";

    /// <summary>
    /// Convertit une date ISO <c>YYYY-MM-DD</c> en libellé français lisible
    /// (« Juin 2025 »). Le mois est forcé en Title Case — <c>MMMM</c> en
    /// fr-FR produit « juin » (minuscule), mais un bulletin officiel
    /// attend la majuscule typographique. Retourne la date brute si le
    /// format est inattendu.
    /// </summary>
    private static string PeriodeLisible(string datePaie)
    {
        if (DateTime.TryParseExact(datePaie, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt))
        {
            var mois = dt.ToString("MMMM", new CultureInfo("fr-FR"));
            var moisCapitalise = char.ToUpper(mois[0], CultureInfo.InvariantCulture) + mois[1..];
            return $"{moisCapitalise} {dt.Year}";
        }
        return datePaie;
    }

    // ----- Sections -----

    private static Action<IContainer> BuildHeader(BulletinAffichage affichage)
    {
        var snapshot = affichage.Snapshot;
        var agent = snapshot.Input.Agent;
        return c => c.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("RÉPUBLIQUE ALGÉRIENNE DÉMOCRATIQUE ET POPULAIRE").SemiBold().FontSize(10);
                row.RelativeItem().AlignRight().Text("Ministère de l'Éducation Nationale").FontSize(9);
            });
            col.Item().Text("BULLETIN DE PAIE").FontSize(14).SemiBold().FontColor(Colors.Blue.Medium);

            // V2 : BulletinId en sous-titre + période lisible.
            var sousTitre = $"Période : {PeriodeLisible(snapshot.Input.DatePaie)}";
            if (!string.IsNullOrWhiteSpace(affichage.BulletinId))
            {
                sousTitre += $"   |   N° {affichage.BulletinId}";
            }
            col.Item().Text(sousTitre).FontSize(9).Italic().FontColor(Colors.Grey.Darken1);

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

    private static Action<IContainer> BuildBody(BulletinAffichage affichage, IReadOnlyList<LigneRappel>? rappels)
    {
        return c => c.Column(col =>
        {
            var bulletin = affichage.Snapshot.Resultat;

            // ----- Détail du bulletin -----
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

            // ----- Totaux du bulletin courant -----
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

            // ----- Section Cumuls annuels (V2) -----
            if (affichage.Cumuls is { } cumuls && cumuls.NombreBulletins > 0)
            {
                col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                col.Item().PaddingTop(4).Text($"Cumuls depuis le 1er janvier {cumuls.Annee}").SemiBold();
                col.Item().Text($"({cumuls.NombreBulletins} bulletin{(cumuls.NombreBulletins > 1 ? "s" : "")} validé{(cumuls.NombreBulletins > 1 ? "s" : "")})")
                    .Italic().FontSize(8).FontColor(Colors.Grey.Medium);
                col.Item().Table(c =>
                {
                    c.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(1);
                    });
                    LigneTotaux(c, "Cumul gains", cumuls.TotalGains.Amount);
                    LigneTotaux(c, "Cumul assiette imposable", cumuls.TotalImposable.Amount);
                    LigneTotaux(c, "Cumul assiette cotisable", cumuls.TotalCotisable.Amount);
                    LigneTotaux(c, "Cumul retenues", cumuls.TotalRetenues.Amount);
                    LigneTotaux(c, "Cumul IRG", cumuls.TotalIrg.Amount);
                    LigneTotaux(c, "Cumul net à payer", cumuls.TotalNet.Amount);
                });
            }

            // ----- Section Rappels (D9) -----
            col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            col.Item().PaddingTop(4).Text("Rappels (évolutions réglementaires rétroactives)").SemiBold();
            if (rappels is null || rappels.Count == 0)
            {
                col.Item().PaddingTop(2).Text("Aucun rappel pour ce bulletin.").Italic()
                    .FontColor(Colors.Grey.Medium).FontSize(8);
            }
            else
            {
                col.Item().Table(rappelsTable =>
                {
                    rappelsTable.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                    });
                    rappelsTable.Header(header =>
                    {
                        header.Cell().Text("Rubrique").SemiBold();
                        header.Cell().AlignRight().Text("Ancien").SemiBold();
                        header.Cell().AlignRight().Text("Nouveau").SemiBold();
                        header.Cell().AlignRight().Text("Delta").SemiBold();
                    });
                    foreach (var ligne in rappels)
                    {
                        rappelsTable.Cell().Text(ligne.RubriqueId);
                        rappelsTable.Cell().AlignRight().Text(Dzd(ligne.MontantAncien.Amount));
                        rappelsTable.Cell().AlignRight().Text(Dzd(ligne.MontantNouveau.Amount));
                        rappelsTable.Cell().AlignRight().Text(DzdSigne(ligne.Delta.Amount));
                    }
                });
            }
        });
    }

    private static Action<IContainer> BuildFooter(BulletinAffichage affichage)
    {
        return c => c.Column(col =>
        {
            // ----- Mentions réglementaires (V2) -----
            col.Item().PaddingTop(6).Text(t =>
            {
                t.Span("Conformément à la réglementation en vigueur. ").Italic().FontSize(7).FontColor(Colors.Grey.Darken1);
                t.Span("Le présent bulletin est à conserver sans limitation de durée. ").Italic().FontSize(7).FontColor(Colors.Grey.Darken1);
                t.Span("Toute modification ultérieure du bulletin initial entraînera l'émission d'un rappel (D9).")
                    .Italic().FontSize(7).FontColor(Colors.Grey.Darken1);
            });
            // ----- Horodatage du snapshot + mention d'inviolabilité -----
            col.Item().Row(row =>
            {
                row.RelativeItem().Text($"Snapshot capturé le {affichage.Snapshot.CapturesLe}")
                    .FontSize(7).FontColor(Colors.Grey.Medium);
                row.RelativeItem().AlignRight().Text("Document généré automatiquement — ne pas modifier")
                    .FontSize(7).FontColor(Colors.Grey.Medium);
            });
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
