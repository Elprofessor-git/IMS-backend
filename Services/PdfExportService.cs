using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Backend_Gestion_Magasin_API.Dtos.Commande;
using Backend_Gestion_Magasin_API.Dtos.Facture;

namespace Backend_Gestion_Magasin_API.Services
{
    /// <summary>
    /// Génération des exports PDF (QuestPDF, licence Community).
    /// Reprend la même préparation de données que l'export Excel (les DTOs sont
    /// construits par les contrôleurs) — aucune logique métier dupliquée ici.
    /// Mise en page volontairement proche des gabarits Excel (pas pixel-perfect).
    /// </summary>
    public class PdfExportService
    {
        static PdfExportService()
        {
            // Licence QuestPDF Community : gratuite pour les sociétés < 1 M$ CA annuel
            // (voir rapport final — consigne de vérification).
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  Facture
        // ─────────────────────────────────────────────────────────────────────────────

        public byte[] ExportFacture(FactureDetailDto d)
        {
            var devise = string.IsNullOrWhiteSpace(d.Devise) ? "EUR" : d.Devise;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.8f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Black));

                    page.Header().Column(header =>
                    {
                        header.Item().AlignCenter().Text($"FACTURE N° {d.NumeroFacture}")
                            .FontSize(16).Bold();

                        header.Item().PaddingTop(4).AlignRight().Text(t =>
                        {
                            t.Span("Date : ").SemiBold();
                            t.Span(d.DateFacture.ToString("dd/MM/yyyy"));
                        });
                    });

                    page.Content().PaddingTop(10).Column(content =>
                    {
                        // Client (nom + adresse).
                        content.Item().Text(t =>
                        {
                            t.Span("Client : ").SemiBold();
                            t.Span(d.ClientNom ?? string.Empty);
                        });
                        content.Item().Text(t =>
                        {
                            t.Span("Adresse : ").SemiBold();
                            t.Span(d.ClientAdresse ?? string.Empty);
                        });

                        // Lignes de facturation : modèle, qté façon, prix façon, montant.
                        content.Item().PaddingTop(12).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3.2f);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(2f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten3).Text("Modèle").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).AlignRight().Text("Qté (façon)").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).AlignRight().Text("Prix façon / pièce").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).AlignRight().Text($"Montant ({devise})").Bold();
                            });

                            foreach (var l in d.Lignes)
                            {
                                table.Cell().Text(l.Modele ?? string.Empty);
                                table.Cell().AlignRight().Text(l.Quantite.ToString());
                                table.Cell().AlignRight().Text(FormatMontant(l.PrixUnitaireFacon));
                                table.Cell().AlignRight().Text(FormatMontant(l.MontantLigne));
                            }

                            // Totaux : « Total Facture » et « Net à payer ».
                            table.Cell().ColumnSpan(3).PaddingTop(2).AlignRight().Text("Total Facture en Euros").Bold();
                            table.Cell().PaddingTop(2).AlignRight().Text(FormatMontant(d.MontantTotal)).Bold();

                            table.Cell().ColumnSpan(3).AlignRight().Text("Net à payer").Bold();
                            table.Cell().AlignRight().Text(FormatMontant(d.MontantTotal)).Bold();
                        });

                        // Colis / poids / volume.
                        var logistique = new List<string>();
                        if (d.NombreColis.HasValue) logistique.Add($"Colis : {d.NombreColis.Value}");
                        if (d.PoidsNetKg.HasValue) logistique.Add($"Poids net : {d.PoidsNetKg.Value:F2} kg");
                        if (d.PoidsBrutKg.HasValue) logistique.Add($"Poids brut : {d.PoidsBrutKg.Value:F2} kg");
                        if (d.VolumeM3.HasValue) logistique.Add($"Volume : {d.VolumeM3.Value:F2} m³");
                        if (logistique.Count > 0)
                            content.Item().PaddingTop(10).Text(string.Join("   ·   ", logistique));

                        // Règlement & livraison.
                        if (!string.IsNullOrWhiteSpace(d.ModePaiement) ||
                            !string.IsNullOrWhiteSpace(d.Rib) ||
                            !string.IsNullOrWhiteSpace(d.Iban) ||
                            !string.IsNullOrWhiteSpace(d.ModeLivraison))
                        {
                            content.Item().PaddingTop(10).Column(reg =>
                            {
                                if (!string.IsNullOrWhiteSpace(d.ModePaiement))
                                {
                                    reg.Item().Text(t =>
                                    {
                                        t.Span("Mode de paiement : ").SemiBold();
                                        t.Span(d.ModePaiement);
                                    });
                                }
                                if (!string.IsNullOrWhiteSpace(d.Rib))
                                {
                                    reg.Item().Text(t =>
                                    {
                                        t.Span("RIB : ").SemiBold();
                                        t.Span(d.Rib);
                                    });
                                }
                                if (!string.IsNullOrWhiteSpace(d.Iban))
                                {
                                    reg.Item().Text(t =>
                                    {
                                        t.Span("IBAN : ").SemiBold();
                                        t.Span(d.Iban);
                                    });
                                }
                                if (!string.IsNullOrWhiteSpace(d.ModeLivraison))
                                {
                                    reg.Item().Text(t =>
                                    {
                                        t.Span("Mode de livraison : ").SemiBold();
                                        t.Span(d.ModeLivraison);
                                    });
                                }
                            });
                        }

                        // Arrêtée en toutes lettres.
                        content.Item().PaddingTop(14).Text(
                            $"Arrêtée la présente facture à la somme de : {ExcelExportService.MontantEnLettres(d.MontantTotal)} EURO ");

                        if (!string.IsNullOrWhiteSpace(d.Notes))
                        {
                            content.Item().PaddingTop(8).Text(t =>
                            {
                                t.Span("Notes : ").SemiBold();
                                t.Span(d.Notes);
                            });
                        }
                    });
                });
            }).GeneratePdf();
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  Rapport de coupe
        // ─────────────────────────────────────────────────────────────────────────────

        public byte[] ExportRapportCoupe(RapportCoupeDto d)
        {
            var modele = d.TitreCommande ?? d.NumeroCommande;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.8f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Black));

                    page.Header().Column(header =>
                    {
                        header.Item().AlignCenter().Text("RAPPORT DE COUPE").FontSize(16).Bold();

                        header.Item().PaddingTop(6).Text(t =>
                        {
                            t.Span("Modèle : ").SemiBold();
                            t.Span(modele);
                        });
                        header.Item().Text(t =>
                        {
                            t.Span("N° de commande : ").SemiBold();
                            t.Span(d.NumeroCommande);
                        });
                        if (!string.IsNullOrWhiteSpace(d.ClientNom))
                        {
                            header.Item().Text(t =>
                            {
                                t.Span("Client : ").SemiBold();
                                t.Span(d.ClientNom);
                            });
                        }
                    });

                    page.Content().PaddingTop(10).Column(content =>
                    {
                        // Répartition par taille.
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.4f);
                                columns.RelativeColumn(1.4f);
                                columns.RelativeColumn(1.4f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten3).Text("Taille").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).AlignRight().Text("Qté commandée").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).AlignRight().Text("Qté coupée").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).AlignRight().Text("Qté exportée").Bold();
                            });

                            foreach (var t in d.Tailles)
                            {
                                table.Cell().Text(t.Taille);
                                table.Cell().AlignRight().Text(t.QuantiteCommande.ToString());
                                table.Cell().AlignRight().Text(t.QuantiteCoupee.ToString());
                                table.Cell().AlignRight().Text(t.QuantiteExportee.ToString());
                            }

                            table.Cell().PaddingTop(2).Text("TOTAL").Bold();
                            table.Cell().PaddingTop(2).AlignRight().Text(d.TotalQuantiteCommande.ToString()).Bold();
                            table.Cell().PaddingTop(2).AlignRight().Text(d.TotalQuantiteCoupee.ToString()).Bold();
                            table.Cell().PaddingTop(2).AlignRight().Text(d.TotalQuantiteExportee.ToString()).Bold();
                        });

                        // Tissus consommables (BOM).
                        content.Item().PaddingTop(14).Text("Consommation tissu / stock restant").FontSize(11).Bold();

                        content.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2.6f); // Désignation
                                columns.RelativeColumn(1f);   // Laize
                                columns.RelativeColumn(1.4f); // Métrage annoncé
                                columns.RelativeColumn(1.2f); // Pièces coupées
                                columns.RelativeColumn(1.3f); // Conso / pièce
                                columns.RelativeColumn(1.3f); // Métrage réel
                                columns.RelativeColumn(1.3f); // Stock restant
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten3).Text("Tissu").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).AlignRight().Text("Laize (m)").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).AlignRight().Text("Métrage annoncé").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).AlignRight().Text("Pièces coupées").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).AlignRight().Text("Conso / pièce").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).AlignRight().Text("Métrage réel").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).AlignRight().Text("Stock restant").Bold();
                            });

                            foreach (var t in d.Tissus)
                            {
                                table.Cell().Text(t.Designation);
                                table.Cell().AlignRight().Text(t.Laize.HasValue ? $"{t.Laize.Value:F2}" : "—");
                                table.Cell().AlignRight().Text($"{t.MetrageAnnonce:F2}");
                                table.Cell().AlignRight().Text(t.QuantiteCoupee.ToString());
                                table.Cell().AlignRight().Text($"{t.ConsoReelle:F2}");
                                table.Cell().AlignRight().Text($"{t.MetrageReelle:F2}");
                                table.Cell().AlignRight().Text($"{t.StockRestant:F2}");
                            }
                        });
                    });
                });
            }).GeneratePdf();
        }

        private static string FormatMontant(decimal valeur) =>
            valeur.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("fr-FR"));
    }
}