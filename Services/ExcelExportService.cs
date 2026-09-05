using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using Backend_Gestion_Magasin_API.Dtos.Commande;
using Backend_Gestion_Magasin_API.Dtos.Facture;

namespace Backend_Gestion_Magasin_API.Services
{
    /// <summary>
    /// Génération des exports Excel à partir des gabarits .xlsx (Templates/).
    /// Les gabarits sont remplis en préservant mise en forme, formules et calculs.
    /// </summary>
    public class ExcelExportService
    {
        private readonly string _templatesDir;

        public ExcelExportService(IWebHostEnvironment env)
        {
            // En local : ContentRootPath/Templates ; en Docker : /app/Templates (copié via csproj).
            _templatesDir = Path.Combine(AppContext.BaseDirectory, "Templates");
            if (!Directory.Exists(_templatesDir))
                _templatesDir = Path.Combine(env.ContentRootPath, "Templates");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  Facture (gabarit « FAC 3318.xlsx »)
        // ─────────────────────────────────────────────────────────────────────────────

        public byte[] ExportFacture(FactureDetailDto d)
        {
            using var wb = new XLWorkbook(Path.Combine(_templatesDir, "FAC_3318.xlsx"));
            var ws = wb.Worksheets.Worksheet(1); // feuille unique « 3257 »

            var date = d.DateFacture.ToString("dd/MM/yyyy");
            ws.Cell("E5").Value = $"Date: {date}";
            ws.Cell("B8").Value = d.ClientNom ?? string.Empty;
            ws.Cell("E8").Value = $"Nom du Client: {d.ClientNom ?? string.Empty}";
            ws.Cell("A9").Value = d.ClientAdresse ?? string.Empty;
            ws.Cell("E9").Value = $"Adresse du Client: {d.ClientAdresse ?? string.Empty}";
            ws.Cell("A13").Value = $"FACTURE N° {d.NumeroFacture}";

            // Lignes : modèle, quantité, prix façon, total façon (=Qté × Prix).
            const int startRow = 16;
            var rows = d.Lignes.Count;
            if (rows == 0) rows = 1; // gabarit : une ligne système sous l'en-tête

            for (var i = 0; i < rows; i++)
            {
                var r = startRow + i;
                if (r > startRow)
                    ws.Row(startRow).CopyTo(ws.Row(r)); // reprend la mise en forme de la 1re ligne

                var ligne = i < d.Lignes.Count ? d.Lignes[i] : null;
                ws.Cell(r, 1).Value = ligne?.Modele ?? string.Empty;
                ws.Cell(r, 2).Value = string.Empty; // composition (non renseignée)
                ws.Cell(r, 3).Value = ligne?.Quantite ?? 0;
                ws.Cell(r, 4).Value = ligne?.PrixUnitaireFacon ?? 0m;
                ws.Cell(r, 6).Value = ligne?.MontantLigne ?? 0m;
            }

            // Totaux : « Total Facture en Euros » et « Net a payer » = montant global.
            ws.Cell("F17").Value = d.MontantTotal;
            ws.Cell("F18").Value = d.MontantTotal;

            // Colis / poids / volume (vierges si non renseignés).
            if (d.NombreColis.HasValue) ws.Cell("C20").Value = d.NombreColis.Value;
            if (d.PoidsNetKg.HasValue) ws.Cell("C21").Value = d.PoidsNetKg.Value;
            if (d.PoidsBrutKg.HasValue) ws.Cell("C22").Value = d.PoidsBrutKg.Value;
            if (d.VolumeM3.HasValue) ws.Cell("C23").Value = d.VolumeM3.Value;

            ws.Cell("B25").Value = d.ModePaiement ?? string.Empty;
            ws.Cell("B26").Value = d.Rib ?? string.Empty;
            ws.Cell("B27").Value = d.Iban ?? string.Empty;
            ws.Cell("B28").Value = d.ModeLivraison ?? string.Empty;

            ws.Cell("A31").Value =
                $"Arrêtée la présente facture à la somme de : {MontantEnLettres(d.MontantTotal)} EURO ";

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  Rapport de coupe (gabarit « RAP DE COUPE - PJAMES+VJAMES-NOIR.xlsx »)
        // ─────────────────────────────────────────────────────────────────────────────

        public byte[] ExportRapportCoupe(RapportCoupeDto d)
        {
            using var wb = new XLWorkbook(Path.Combine(_templatesDir, "RapportCoupe_PJAMES-NOIR.xlsx"));

            // Une seule commande → une seule feuille, nommée d'après le modèle.
            while (wb.Worksheets.Count > 1)
                wb.Worksheets.Worksheet(2).Delete();
            var ws = wb.Worksheets.Worksheet(1);
            ws.Name = NomFeuille(d.TitreCommande ?? d.NumeroCommande);

            var modele = d.TitreCommande ?? d.NumeroCommande;
            ws.Cell("B3").Value = modele;
            ws.Cell("H3").Value = d.TotalQuantiteCommande;
            ws.Cell("A4").Value = "N°DE COMMANDE";
            ws.Cell("H4").Value = d.NumeroCommande;

            // ── Répartition par taille (colonnes 34 → 44) ──
            var colonnesTailles = new List<(string Cell, int Taille)>
            {
                ("C", 12), ("D", 12), ("E", 12), ("F", 12), ("G", 12), ("H", 12),
            };
            // En-têtes réels du gabarit : C12..H12 = 34, 36, 38, 40, 42, 44.
            var parTaille = new Dictionary<int, RapportCoupeTailleDto>();
            foreach (var t in d.Tailles)
            {
                if (TailleNumerique(t.Taille) is int n)
                    parTaille[n] = t;
            }

            for (int col = 0; col < 6; col++)
            {
                var letter = (char)('C' + col);
                var header = ws.Cell($"{letter}12").GetValue<double>();
                var taille = (int)header;
                var ligne = parTaille.TryGetValue(taille, out var t) ? t : null;

                ws.Cell($"{letter}13").Value = ligne?.QuantiteCommande ?? 0; // Quantité commande
                ws.Cell($"{letter}14").Value = ligne?.QuantiteCoupee ?? 0;   // Quantité coupée
                ws.Cell($"{letter}15").Value = 0;                            // TDS
                ws.Cell($"{letter}16").Value = ligne?.QuantiteExportee ?? 0; // Quantité livrée
                ws.Cell($"{letter}18").Value = 0;                            // Quantité livrée ++
            }

            // Totaux (colonne L).
            ws.Cell("L13").Value = d.TotalQuantiteCommande;
            ws.Cell("L14").Value = d.TotalQuantiteCoupee;
            ws.Cell("L15").Value = 0;
            ws.Cell("L16").Value = d.TotalQuantiteExportee;
            ws.Cell("L18").Value = 0;

            // ── Tissus consommables (BOM) : une ligne par tissu, à partir de la ligne 7 ──
            const int tissuStart = 7;
            var tissus = d.Tissus;
            for (var i = 0; i < tissus.Count; i++)
            {
                var r = tissuStart + i;
                if (r > tissuStart)
                    ws.Row(tissuStart).CopyTo(ws.Row(r));

                var t = tissus[i];
                ws.Cell(r, 1).Value = t.Designation;
                ws.Cell(r, 2).Value = t.Laize ?? 0m;
                ws.Cell(r, 3).Value = t.MetrageAnnonce;
                ws.Cell(r, 4).Value = t.QuantiteCoupee;                  // Qtés coupées
                ws.Cell(r, 5).Value = t.ConsoReelle;                     // Conso réelle (par pièce)
                ws.Cell(r, 6).Value = t.MetrageReelle;                   // Métrage réel
                ws.Cell(r, 7).Value = 0m;                                // Manque tissu
                ws.Cell(r, 8).Value = 0m;                                // Défaut tissu
                ws.Cell(r, 9).Value = 0m;                                // Retrait
                ws.Cell(r, 12).Value = t.StockRestant;                   // Stock
            }

            // Lignes tissu résiduelles du gabarit (plus de tissus déclarés qu'attendu) : on vide.
            for (var r = tissuStart + tissus.Count; r <= tissuStart + 2; r++)
            {
                if (ws.LastRowUsed()?.RowNumber() is int last && r > last) break;
                ws.Cell(r, 1).Value = string.Empty;
                ws.Cell(r, 2).Value = string.Empty;
                ws.Cell(r, 3).Value = string.Empty;
                ws.Cell(r, 4).Value = string.Empty;
                ws.Cell(r, 5).Value = string.Empty;
                ws.Cell(r, 6).Value = string.Empty;
                ws.Cell(r, 12).Value = string.Empty;
            }

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────────────

        private static string NomFeuille(string? nom)
        {
            var s = (nom ?? "COUPE").Trim();
            foreach (var c in "[]:*?/\\")
                s = s.Replace(c.ToString(), "-");
            return s.Length > 31 ? s[..31] : s;
        }

        private static int? TailleNumerique(string taille)
        {
            var n = new string(taille.Where(char.IsDigit).ToArray());
            return n.Length > 0 && int.TryParse(n, out var v) ? v : null;
        }

        public static string MontantEnLettres(decimal montant)
        {
            var euros = (long)Math.Floor(Math.Abs(montant));
            var centimes = (int)Math.Round((Math.Abs(montant) * 100) % 100);

            var texte = euros == 0
                ? "ZERO"
                : NombreEnLettres(euros).ToUpperInvariant();
            if (centimes > 0)
                texte += $" ET {NombreEnLettres(centimes).ToUpperInvariant()} CENTIMES";

            return montant < 0 ? "MOINS " + texte : texte.Trim();
        }

        private static readonly string[] _unites =
            { "", "UN", "DEUX", "TROIS", "QUATRE", "CINQ", "SIX", "SEPT", "HUIT", "NEUF",
              "DIX", "ONZE", "DOUZE", "TREIZE", "QUATORZE", "QUINZE", "SEIZE",
              "DIX-SEPT", "DIX-HUIT", "DIX-NEUF" };

        private static readonly string[] _dizaines =
            { "", "DIX", "VINGT", "TRENTE", "QUARANTE", "CINQUANTE", "SOIXANTE" };

        private static string NombreEnLettres(long n)
        {
            if (n == 0) return "ZERO";
            if (n >= 1_000_000_000_000) return "VALEUR TROP GRANDE";

            var parts = new List<string>();
            var milliards = n / 1_000_000_000;
            n %= 1_000_000_000;
            var millions = n / 1_000_000;
            n %= 1_000_000;
            var milliers = n / 1_000;
            n %= 1_000;

            if (milliards > 0) parts.Add($"{SousMille(milliards)} MILLIARDS");
            if (millions > 0) parts.Add(millions == 1 ? "UN MILLION" : $"{SousMille(millions)} MILLIONS");
            if (milliers > 0) parts.Add(milliers == 1 ? "MILLE" : $"{SousMille(milliers)} MILLE");
            if (n > 0) parts.Add(SousMille(n));

            return string.Join(' ', parts);
        }

        private static string SousMille(long n)
        {
            if (n == 0) return string.Empty;

            var r = new List<string>();
            var cent = n / 100;
            n %= 100;
            if (cent > 0)
            {
                r.Add(cent == 1 ? "CENT" : $"{_unites[cent]} CENT");
                if (n == 0 && cent > 1) r[^1] += "S"; // « deux cents »
            }
            if (n == 0) return string.Join(' ', r);

            switch (n)
            {
                case 70:
                    r.Add("SOIXANTE-DIX");
                    break;
                case 71:
                    r.Add("SOIXANTE ET ONZE");
                    break;
                case 80:
                    r.Add("QUATRE-VINGTS");
                    break;
                case 90:
                    r.Add("QUATRE-VINGT-DIX");
                    break;
                default:
                    if (n < 20)
                    {
                        r.Add(_unites[n]);
                    }
                    else if (n < 80)
                    {
                        var dix = n / 10;
                        var u = n % 10;
                        var mot = dix == 7 ? "SOIXANTE" : _dizaines[dix];
                        r.Add(u == 0 ? mot : u == 1 ? $"{mot} ET {_unites[u]}" : $"{mot}-{_unites[u]}");
                    }
                    else // 81..89, 91..99
                    {
                        var u = n - 80; // 1..9 ou 11..19
                        r.Add(u < 10 ? $"QUATRE-VINGT-{_unites[u]}" : $"QUATRE-VINGT-{_unites[u]}");
                    }
                    break;
            }

            return string.Join(' ', r);
        }
    }
}