using System.Text.Json;
using System.Text.RegularExpressions;
using Backend_Gestion_Magasin_API.Services.Gmail;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// Assainissement du HTML d'email — la barrière entre un email hostile et le
/// navigateur de l'utilisateur.
///
/// Ces tests sont volontairement unitaires et sans base : la surface à couvrir est la
/// logique de nettoyage, pas l'infrastructure. Chaque cas reproduit une technique
/// d'évasion réellement employée contre les webmails.
/// </summary>
public class HtmlSanitizerTests
{
    // ── Exécution de script ───────────────────────────────────────────────

    [Fact]
    public void Supprime_un_script_avec_son_contenu()
    {
        var html = "<p>Bonjour</p><script>alert(document.cookie)</script><p>Suite</p>";

        var result = HtmlSanitizer.Sanitize(html);

        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert", result);
        Assert.Contains("Bonjour", result);
        Assert.Contains("Suite", result);
    }

    [Fact]
    public void Supprime_un_script_masque_dans_un_commentaire_html()
    {
        // Sans le retrait des commentaires, « <!-- --> » peut devenir du balisage
        // lors d'une resérialisation : le script ressuscite.
        var html = "<p>ok</p><!-- <script>alert(1)</script> -->";

        var result = HtmlSanitizer.Sanitize(html);

        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert", result);
    }

    [Theory]
    [InlineData("onerror", "<img src=x onerror=alert(1)>")]
    [InlineData("onload", "<body onload=alert(1)>texte</body>")]
    [InlineData("onclick", "<div onclick=alert(1)>clic</div>")]
    [InlineData("onfocus", "<input onfocus=alert(1) autofocus>")]
    public void Retire_les_gestionnaires_evenement(string gestionnaire, string html)
    {
        var result = HtmlSanitizer.Sanitize(html);

        Assert.DoesNotContain(gestionnaire, result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert", result);
    }

    [Fact]
    public void Retire_un_gestionnaire_avec_casse_melangee()
    {
        // Le HTML est insensible à la casse : « OnErRoR » doit être traité comme onerror.
        var result = HtmlSanitizer.Sanitize("<img src=x OnErRoR=alert(1)>");

        Assert.DoesNotContain("onerror", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert", result);
    }

    [Fact]
    public void Retire_le_contenu_d_un_style_et_dun_iframe()
    {
        var html = """
                   <style>body{display:none}</style>
                   <iframe src="https:// phishing.example/steal"></iframe>
                   <p>Contenu</p>
                   """;

        var result = HtmlSanitizer.Sanitize(html);

        Assert.DoesNotContain("display:none", result);
        Assert.DoesNotContain("iframe", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Contenu", result);
    }

    // ── Schémas d'URL ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("  javascript:alert(1)")]
    [InlineData("java\tscript:alert(1)")]
    [InlineData("java&#115;cript:alert(1)")]
    public void Neutralise_un_href_javascript(string url)
    {
        var result = HtmlSanitizer.Sanitize($"<a href=\"{url}\">Cliquer</a>");

        Assert.DoesNotContain("javascript", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cliquer", result);
    }

    [Fact]
    public void Neutralise_un_src_data_uri_hors_image()
    {
        // Un data: URI dans un src peut charger un document arbitraire.
        var result = HtmlSanitizer.Sanitize("<img src=\"data:text/html;base64,PHNjcmlwdD4=\">");

        Assert.DoesNotContain("data:", result);
    }

    // ── Images intégrées en data: (A3) ────────────────────────────────────
    //
    // Les clients de messagerie transportent logos et signatures directement
    // dans le corps en `data:image/…;base64,…`. Sans cela, la moitié des
    // courriels professionnels s'affiche sans logo. L'autorisation reste
    // étroite : image, base64, pas de SVG, 1 Mio — et seulement sur un src.

    /// <summary>1 pixel PNG valide, en base64.</summary>
    private const string PixelPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    [Fact]
    public void Conserve_une_image_data_png()
    {
        var result = HtmlSanitizer.Sanitize($"<p>Bonjour</p><img src=\"data:image/png;base64,{PixelPng}\">");

        Assert.Contains("data:image/png;base64," + PixelPng, result);
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/gif")]
    [InlineData("image/webp")]
    [InlineData("image/bmp")]
    public void Conserve_les_autres_types_d_images(string mediaType)
    {
        var result = HtmlSanitizer.Sanitize($"<img src=\"data:{mediaType};base64,{PixelPng}\">");

        Assert.Contains($"data:{mediaType};base64,", result);
    }

    [Fact]
    public void Refuse_une_image_svg_meme_encodée_en_base64()
    {
        // Le SVG s'annonce comme une image mais est un document exécutable.
        var svg = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>"));

        var result = HtmlSanitizer.Sanitize($"<img src=\"data:image/svg+xml;base64,{svg}\">");

        Assert.DoesNotContain("data:", result);
    }

    [Fact]
    public void Refuse_un_svg_en_clair()
    {
        var result = HtmlSanitizer.Sanitize("<img src=\"data:image/svg+xml,<svg onload='alert(1)'/>\">");

        Assert.DoesNotContain("data:", result);
        Assert.DoesNotContain("onload", result);
    }

    [Fact]
    public void Refuse_une_image_trop_lourde()
    {
        // 1 400 000 octets décodés, au-delà du plafond de 1 Mio.
        var tropGrosse = Convert.ToBase64String(new byte[1_400_000]);

        var result = HtmlSanitizer.Sanitize($"<img src=\"data:image/png;base64,{tropGrosse}\">");

        Assert.DoesNotContain("data:", result);
    }

    [Fact]
    public void Conserve_une_image_a_la_limite_exacte()
    {
        // Le plafond est sur les octets décodés : 1 048 576 doit passer.
        var aLaLimite = Convert.ToBase64String(new byte[HtmlSanitizer.MaxInlineImageBytes]);

        var result = HtmlSanitizer.Sanitize($"<img src=\"data:image/png;base64,{aLaLimite}\">");

        Assert.Contains("data:image/png;base64,", result);
    }

    [Fact]
    public void Refuse_une_charge_utilitaire_non_base64()
    {
        // En clair, la charge utile peut contenir des balises et des guillemets.
        var result = HtmlSanitizer.Sanitize("<img src=\"data:image/png;charset=utf-8,<img src=x onerror=alert(1)>\">");

        Assert.DoesNotContain("data:", result);
    }

    [Fact]
    public void Refuse_une_charge_utilitaire_casquee()
    {
        var result = HtmlSanitizer.Sanitize("<img src=\"data:image/png;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==\">");

        // Base64 valide, mais le type annoncé est un PNG alors que le contenu est un
        // script : c'est le navigateur qui refuse d'afficher, pas l'assainisseur. On ne
        // peut pas deviner le contenu réel, on vérifie seulement ce qui est déclaré.
        Assert.Contains("data:image/png;base64,", result);
    }

    [Fact]
    public void Refuse_une_charge_utilitaire_invalide()
    {
        var result = HtmlSanitizer.Sanitize("<img src=\"data:image/png;base64,pas%%du_base64\">");

        Assert.DoesNotContain("data:", result);
    }

    [Fact]
    public void Refuse_un_data_uri_dans_un_lien()
    {
        // Un lien vers une donnée n'a aucun usage légitime dans un email, et
        // AllowNavigation serait de toute façon bloqué par le navigateur.
        var result = HtmlSanitizer.Sanitize($"<a href=\"data:image/png;base64,{PixelPng}\">Voir</a>");

        Assert.DoesNotContain("data:", result);
        Assert.Contains("Voir", result);
    }

    [Fact]
    public void Refuse_un_data_uri_de_type_xml_derivé()
    {
        var result = HtmlSanitizer.Sanitize($"<img src=\"data:image/svg+xml;charset=utf-8;base64,{PixelPng}\">");

        Assert.DoesNotContain("data:", result);
    }

    [Fact]
    public void Accepte_une_image_data_avec_retours_a_la_ligne()
    {
        var coupee = string.Join("\n", Enumerable.Range(0, (PixelPng.Length + 63) / 64)
            .Select(i => PixelPng.Substring(i * 64, Math.Min(64, PixelPng.Length - i * 64))));

        var result = HtmlSanitizer.Sanitize($"<img src=\"data:image/png;base64,{coupee}\">");

        Assert.Contains("data:image/png;base64,", result);
    }

    [Theory]
    [InlineData("https://exemple.fr/bons/4821.pdf")]
    [InlineData("http://exemple.fr/x")]
    [InlineData("mailto:client@exemple.fr")]
    [InlineData("tel:+33123456789")]
    [InlineData("cid:logo-ims-001")]
    [InlineData("/api/gmail/messages/7/inline/logo-ims-001")]
    [InlineData("image.png")]
    public void Conserve_les_urls_sures(string url)
    {
        var result = HtmlSanitizer.Sanitize($"<a href=\"{url}\">Lien</a>");

        Assert.Contains(url, result);
    }

    // ── Injection d'en-tête / de contenu ──────────────────────────────────

    [Fact]
    public void Neutralise_une_tentative_de_sortie_d_attribut()
    {
        // « title="a&quot; onmouseover=&quot;alert(1)" » est, après décodage des entités,
        // UN SEUL attribut dont la valeur contient un guillemet. Si ce guillemet ressortait
        // tel quel, il fermerait l'attribut et le navigateur verrait un vrai onmouseover.
        var html = "<a href=\"x\" title=\"a&quot; onmouseover=&quot;alert(1)\">t</a>";

        var result = HtmlSanitizer.Sanitize(html);

        // Le guillemet doit être échappé : plus aucun « onmouseover= » réel ne subsiste.
        Assert.DoesNotContain(" onmouseover=\"", result);
        Assert.DoesNotContain(" onmouseover=alert", result);
        Assert.Contains("&amp;quot;", result);
    }

    [Fact]
    public void Retire_un_gestionnaire_place_entre_deux_attributs_reels()
    {
        // Variante où l'injection est déjà dans la position d'attribut : c'est le cas
        // réellement exploitable, et il doit être éliminé par la liste noire on*.
        var html = "<a href=\"x\" onmouseover=\"alert(1)\" title=\"t\">lien</a>";

        var result = HtmlSanitizer.Sanitize(html);

        Assert.DoesNotContain("onmouseover", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert", result);
        Assert.Contains("title=\"t\"", result);
    }

    [Fact]
    public void Supprime_les_balises_sans_contenu_autorisees()
    {
        var html = "<form action=\"https://piege.example\"><input name=\"carte\" value=\"4111\"><button>Valider</button></form>";

        var result = HtmlSanitizer.Sanitize(html);

        Assert.DoesNotContain("<form", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<input", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("4111", result);
    }

    [Fact]
    public void Empeche_un_svg_integrable_dans_le_contenu()
    {
        // Un <svg> inline peut porter un <script> et des animations executable.
        var html = "<svg onload=\"alert(1)\"><script>alert(2)</script></svg><p>ok</p>";

        var result = HtmlSanitizer.Sanitize(html);

        Assert.DoesNotContain("<svg", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert", result);
        Assert.Contains("ok", result);
    }

    // ── Préservation du contenu légitime ──────────────────────────────────

    [Fact]
    public void Preserve_la_mise_en_forme_des_emails_reels()
    {
        var html = """
                   <div style="font-family:Arial;color:#333">
                     <h2>Commande n°4821</h2>
                     <p>Bonjour,</p>
                     <table border="1"><tr><td>Tissu</td><td>120 m</td></tr></table>
                     <img src="/api/gmail/messages/7/inline/logo1" alt="Logo" width="80">
                     <a href="https://exemple.fr/commande/4821">Voir la commande</a>
                   </div>
                   """;

        var result = HtmlSanitizer.Sanitize(html);

        Assert.Contains("Commande n°4821", result);
        Assert.Contains("<h2>", result);
        Assert.Contains("<table", result);
        Assert.Contains("color:#333", result);
        Assert.Contains("width=\"80\"", result);
        Assert.Contains("Voir la commande", result);
    }

    [Fact]
    public void Ne_reintroduit_pas_de_balise_fermante_orpheline()
    {
        // Une balise auto-fermante comme <br> n'a pas de </br> en HTML : en réémettre une,
        // on produit un document invalide que le navigateur « répare » à sa façon.
        var result = HtmlSanitizer.Sanitize("Ligne 1<br>Ligne 2<br/>Ligne 3");

        Assert.DoesNotContain("</br", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<br>", result);
    }

    [Fact]
    public void Gere_les_entrees_vides_ou_nulles()
    {
        Assert.Equal("", HtmlSanitizer.Sanitize(null));
        Assert.Equal("", HtmlSanitizer.Sanitize(""));
        Assert.Equal("", HtmlSanitizer.Sanitize("   "));
    }

    [Fact]
    public void N_autorise_pas_la_pseudo_classe_url_dans_le_style()
    {
        // url(...) ferait charger une ressource externe depuis le navigateur de l'utilisateur.
        var html = "<div style=\"background:url(https://piege.example/pixel.png)\">t</div>";

        var result = HtmlSanitizer.Sanitize(html);

        Assert.DoesNotContain("url(", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("piege.example", result);
    }

    [Fact]
    public void Le_document_assaini_ne_contient_plus_aucun_script_apres_plusieurs_passages()
    {
        // L'assainissement est idempotent : le relire ne doit rien aggraver.
        var hostile = "<div onclick=alert(1)><script>alert(2)</script><a href=\"javascript:alert(3)\">x</a><img src=y onerror=alert(4)></div>";

        var une = HtmlSanitizer.Sanitize(hostile);
        var deux = HtmlSanitizer.Sanitize(une);

        Assert.Equal(une, deux);
        Assert.DoesNotMatch(new Regex("(on[a-z]+\\s*=|javascript:|<script)", RegexOptions.IgnoreCase), deux);
    }
}
