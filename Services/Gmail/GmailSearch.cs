using Backend_Gestion_Magasin_API.Models.Gmail;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Services.Gmail
{
    /// <summary>
    /// Recherche plein texte des emails, partagée par la liste paginée et la
    /// synchronisation automatique : les deux doivent trouver exactement le même
    /// ensemble, sinon un email remonté par la sync disparaît de la recherche.
    /// </summary>
    public static class GmailSearch
    {
        /// <summary>
        /// Construit le motif ILike pour un terme libre.
        /// <para>
        /// Deux corrections indissociables :
        /// <list type="bullet">
        /// <item><c>EF.Functions.ILike</c> est insensible à la casse (contrairement à
        /// <c>string.Contains</c>, que Npgsql traduit par <c>strpos</c>, sensible à la
        /// casse) : sans cela, « tissu » ne trouvait pas « Tissu ».</item>
        /// <item>Les jokers <c>%</c> et <c>_</c> saisis par l'utilisateur sont
        /// échappés : sans cela, « 100% » ou « a_b » transformait la recherche en
        /// motif global, renvoyant des milliers de lignes.</item>
        /// </list>
        /// </para>
        /// </summary>
        public static string BuildPattern(string term) =>
            "%" + EscapeLikePattern(term.Trim()) + "%";

        /// <summary>Échappe les jokers LIKE/ILIKE ainsi que le caractère d'échappement lui-même.</summary>
        public static string EscapeLikePattern(string value) =>
            value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

        /// <summary>
        /// Filtre les messages sur l'expéditeur, l'objet, l'extrait ET le corps complet
        /// (l'extrait est tronqué par Gmail à ~100 caractères : sans BodyText, un terme
        /// situé plus bas dans le message était introuvable).
        /// </summary>
        public static IQueryable<GmailMessage> Apply(
            IQueryable<GmailMessage> source, string? search, string pattern)
        {
            if (string.IsNullOrWhiteSpace(search)) return source;

            return source.Where(m =>
                EF.Functions.ILike(m.From, pattern, "\\") ||
                EF.Functions.ILike(m.Subject, pattern, "\\") ||
                EF.Functions.ILike(m.Snippet, pattern, "\\") ||
                EF.Functions.ILike(m.BodyText, pattern, "\\"));
        }
    }

    /// <summary>
    /// Réécrit les images intégrées d'un email.
    /// <para>
    /// Un corps HTML d'email référence ses logos et signatures par <c>src="cid:..."</c>, un
    /// identifiant local au message. Le navigateur n'a aucun moyen de résoudre ce
    /// identifiant, donc les images s'afficheraient cassées. On les pointe vers un endpoint
    /// IMS qui relaie le flux depuis Gmail : les images restent donc confinées à la boîte
    /// Gmail de leur propriétaire et ne sont jamais stockées.
    /// </para>
    /// </summary>
    public static class GmailInlineImageRewriter
    {
        private static readonly System.Text.RegularExpressions.Regex CidSource = new(
            """(?<prefix>\ssrc\s*=\s*)(?<quote>["'])\s*cid:(?<cid>[^"']*)\s*\k<quote>""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        public static string Rewrite(string? html, int imsMessageId)
        {
            if (string.IsNullOrWhiteSpace(html)) return "";

            return CidSource.Replace(html, m =>
            {
                var cid = m.Groups["cid"].Value.Trim();
                if (cid.Length == 0) return m.Value;

                // Le Content-ID est réinjecté dans l'URL : sans encodage, un identifiant
                // contenant « / » ou « ? » transformerait la requête en autre chose.
                var url = $"/api/gmail/messages/{imsMessageId}/inline/{Uri.EscapeDataString(cid)}";
                return $"{m.Groups["prefix"].Value}{m.Groups["quote"].Value}{url}{m.Groups["quote"].Value}";
            });
        }
    }
}
