using Backend_Gestion_Magasin_API.Models.Gmail;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// Qualification des pièces jointes AU NIVEAU DU SERVICE (A2).
/// <para>
/// <see cref="GmailAttachmentQualificationTests"/> prouve le comportement de bout en
/// bout via le double enregistreur, mais le double court-circuite le service : le
/// parcours MIME — lecture de <c>Content-Disposition</c>, remontée de l'arbre des
/// parties, comparaison des <c>cid:</c> — n'y est pas exercé. C'est pourtant
/// exactement là que se situait le défaut.
/// </para>
/// <para>
/// Ces tests passent par <see cref="GmailChargeFactice"/>, qui instancie le vrai
/// <c>GmailApiService</c> avec un gestionnaire HTTP renvoyant un payload Gmail fabriqué :
/// la charge utile est réaliste (parties imbriquées, en-têtes,
/// <c>body.attachmentId</c>), mais AUCUN appel ne sort.
/// </para>
/// </summary>
public class GmailApiAttachmentParsingTests
{
    // ── 1. Le cas du rapport : PDF « attachment » PORTE un Content-ID ────────

    [Fact]
    public void Piece_Content_Disposition_attachment_avec_Content_ID_nest_pas_inline()
    {
        var json = GmailChargeFactice.Message(
            "<p>Bonjour, voici le document.</p>",
            GmailChargeFactice.Part("2", "application/pdf", "attachment", "doc-interne", "plan-de-coupe.pdf"));

        var (message, _) = GmailChargeFactice.Lire(json);

        var pdf = Assert.Single(message.Attachments);
        Assert.False(pdf.IsInline);
        Assert.Null(pdf.ContentId);
        Assert.True(message.HasAttachments);
    }

    // ── 2. Une image réellement référencée par cid: reste inline ────────────

    [Fact]
    public void Image_inline_referencee_par_un_cid_dans_le_html_reste_inline()
    {
        var json = GmailChargeFactice.Message(
            """<p>Bonjour <img src="cid:logo-signature"></p>""",
            GmailChargeFactice.Part("2", "image/png", "inline", "logo-signature", ""));

        var (message, _) = GmailChargeFactice.Lire(json);

        var logo = Assert.Single(message.Attachments);
        Assert.True(logo.IsInline);
        Assert.Equal("logo-signature", logo.ContentId);
        Assert.False(message.HasAttachments);
    }

    // ── 3. Une image avec Content-ID mais JAMAIS référencée : pièce jointe ──

    [Fact]
    public void Image_avec_Content_ID_non_referencee_est_classee_piece_jointe()
    {
        var json = GmailChargeFactice.Message(
            "<p>Aucun lien vers l'image.</p>",
            GmailChargeFactice.Part("2", "image/png", "inline", "logo-orphelin", ""));

        var (message, _) = GmailChargeFactice.Lire(json);

        var image = Assert.Single(message.Attachments);
        Assert.False(image.IsInline);
        Assert.Null(image.ContentId);
        Assert.True(message.HasAttachments);
    }

    // ── 4. Content-Disposition « attachment » l'emporte même si le cid: est
    //       RÉELLEMENT référencé par le HTML (pièce jointe illustrée) ────────

    [Fact]
    public void Disposition_attachment_lemporte_meme_si_le_cid_est_reference()
    {
        var json = GmailChargeFactice.Message(
            """<p><img src="cid:illustration"></p>""",
            GmailChargeFactice.Part("2", "image/jpeg", "attachment", "illustration", "schema.jpg"));

        var (message, _) = GmailChargeFactice.Lire(json);

        var illustration = Assert.Single(message.Attachments);
        Assert.False(illustration.IsInline);
        Assert.Null(illustration.ContentId);
    }

    // ── 5. Un cid: orphelin est retiré du corps (pas d'image cassée) ─────────

    [Fact]
    public void Reference_cid_orpheline_retiree_du_corps()
    {
        var json = GmailChargeFactice.Message(
            """<p>Avant<img src="cid:absent">Après</p>""",
            GmailChargeFactice.Part("2", "image/png", "inline", "autre", ""));

        var (message, _) = GmailChargeFactice.Lire(json);

        Assert.DoesNotContain("cid:absent", message.BodyHtml!);
        // La partie réellement présente n'est PAS inline non plus : son cid: n'est pas
        // référencé, donc elle devient une pièce jointe visible.
        Assert.All(message.Attachments, a => Assert.False(a.IsInline));
    }

    // ── 6. Un cid: résolu est conservé tel quel dans le corps ───────────────

    [Fact]
    public void Reference_cid_resolue_conservee_dans_le_corps()
    {
        var json = GmailChargeFactice.Message(
            """<p><img src="cid:logo"></p>""",
            GmailChargeFactice.Part("2", "image/png", "inline", "logo", ""));

        var (message, _) = GmailChargeFactice.Lire(json);

        Assert.Contains("cid:logo", message.BodyHtml!);
    }

    // ── 7. Partie non-image référencée : jamais inline ──────────────────────

    [Fact]
    public void Partie_non_image_referencee_par_cid_nest_pas_inline()
    {
        var json = GmailChargeFactice.Message(
            """<p><a href="cid:rapport">Télécharger</a></p>""",
            GmailChargeFactice.Part("2", "application/pdf", "inline", "rapport", "rapport.pdf"));

        var (message, _) = GmailChargeFactice.Lire(json);

        var doc = Assert.Single(message.Attachments);
        Assert.False(doc.IsInline);
    }

    // ── 8. Parties imbriquées (multipart dans multipart) ────────────────────

    [Fact]
    public void Les_parties_imbriquees_sont_parcourues()
    {
        var imbrique = $$"""
            {
              "partId": "3",
              "mimeType": "multipart/related",
              "headers": [{{GmailChargeFactice.EnTete("Content-Type", "multipart/related; boundary=in")}}, {{GmailChargeFactice.EnTete("Content-Disposition", "inline")}}],
              "body": { "size": 0 },
              "parts": [ {{GmailChargeFactice.Part("3.1", "image/png", "inline", "logo-imprime", "")}} ]
            }
            """;

        var json = GmailChargeFactice.Message("""<p><img src="cid:logo-imprime"></p>""", imbrique);

        var (message, _) = GmailChargeFactice.Lire(json);

        var logo = Assert.Single(message.Attachments);
        Assert.True(logo.IsInline);
        Assert.Equal("logo-imprime", logo.ContentId);
    }

    // ── 9. Un message sans pièce : aucune icône trombone fantôme ────────────

    [Fact]
    public void Message_sans_piece_signale_aucune_piece_jointe()
    {
        var (message, urls) = GmailChargeFactice.Lire(GmailChargeFactice.Message("<p>Bonjour.</p>"));

        Assert.Empty(message.Attachments);
        Assert.False(message.HasAttachments);
        // Une seule lecture : la maintenance ne doit pas coûter un second aller-retour
        // quand la synchronisation normale suffit.
        Assert.Single(urls);
    }
}
