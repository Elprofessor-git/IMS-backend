using System.Text;
using System.Text.RegularExpressions;

namespace Backend_Gestion_Magasin_API.Services.Gmail
{
    /// <summary>
    /// Assainissement du HTML d'email avant stockage et affichage.
    /// <para>
    /// Le HTML reçu d'un expéditeur est du code hostile jusqu'à preuve du contraire : un
    /// email peut contenir <c>&lt;script&gt;</c>, un <c>onerror</c> sur une image, un
    /// <c>javascript:</c> ou un iframe de hameçonnage. Rien de tout cela n'est affiché tel
    /// quel dans l'application.
    /// </para>
    /// <para>
    /// Le HTML n'est JAMAIS renvoyé au frontend tel que reçu : cette classe est appelée
    /// une fois à la réception (<c>GmailSyncService</c>) et une seconde fois à la lecture
    /// (<c>GmailController</c>), défense en profondeur qui couvre aussi les lignes
    /// synchronisées avant l'introduction de cette classe.
    /// </para>
    /// </summary>
    public static class HtmlSanitizer
    {
        /// <summary>
        /// Éléments MIS À SUPPRIMER avec leur contenu : ils ne dégradent pas la lecture
        /// et sont les vecteurs d'exécution de code classiques.
        /// </summary>
        private static readonly string[] DangerousElements =
        {
            "script", "style", "iframe", "frame", "frameset", "object", "embed",
            "applet", "form", "input", "button", "select", "option", "textarea",
            "link", "meta", "base", "svg", "math", "template", "noscript", "head"
        };

        /// <summary>
        /// Attributs retirés partout : les gestionnaires d'événements (on*) et les attributs
        /// qui réorientent la navigation ou l'URL de base de la page.
        /// </summary>
        private static readonly HashSet<string> ForbiddenAttributes = new(StringComparer.OrdinalIgnoreCase)
        {
            "srcdoc", "formaction", "action", "ping", "http-equiv", "dynsrc", "lowsrc"
        };

        /// <summary>Schémas d'URL autorisés dans un href/src. Tout le reste est neutralisé.</summary>
        private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
        {
            "http", "https", "mailto", "tel", "cid"
        };

        // Nettoyage préalable des constructions que l'analyseur regex ne verrait pas
        // correctement (commentaires, CDATA) : sans cela, « <!-- <script> --> » peut
        // devenir un vrai script une fois resérialisé.
        private static readonly Regex Comments = new(@"<!--.*?-->", RegexOptions.Singleline);
        private static readonly Regex Cdata = new(@"<!\[CDATA\[.*?\]\]>", RegexOptions.Singleline);
        private static readonly Regex Doctype = new(@"<!DOCTYPE[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex TagPattern = new(
            @"<\s*(?<slash>/?)\s*(?<name>[a-zA-Z][a-zA-Z0-9-]*)(?<attrs>(?:[^>""']|""[^""]*""|'[^']*')*)(?<selfclose>/?)\s*>",
            RegexOptions.Compiled | RegexOptions.Singleline);

        public static string Sanitize(string? html)
        {
            if (string.IsNullOrWhiteSpace(html)) return "";

            var working = Doctype.Replace(html, "");
            working = Cdata.Replace(working, "");
            working = Comments.Replace(working, "");

            var output = new StringBuilder(working.Length);
            var position = 0;
            var skipDepth = 0;
            var skipTag = "";

            foreach (Match match in TagPattern.Matches(working))
            {
                // ATTENTION : en .NET, Groups["x"].Success vaut TRUE pour un groupe construit
                // sur un quantificateur optionnel (`/?`) — il a « participé » au match en
                // matchant la chaîne vide. Tester Success ferait donc passer TOUTE balise
                // pour une balise fermante, et le HTML ressortirait vide. Il faut tester
                // la valeur capturée.
                var isClosing = match.Groups["slash"].Value.Length > 0;
                var isSelfClosing = match.Groups["selfclose"].Value.Length > 0;

                if (skipDepth > 0)
                {
                    // À l'intérieur d'un élément supprimé : on jette tout jusqu'à sa fermeture.
                    if (isClosing)
                    {
                        if (match.Groups["name"].Value.Equals(skipTag, StringComparison.OrdinalIgnoreCase))
                            skipDepth--;
                    }
                    else if (!isSelfClosing
                             && match.Groups["name"].Value.Equals(skipTag, StringComparison.OrdinalIgnoreCase))
                    {
                        skipDepth++;
                    }

                    position = match.Index + match.Length;
                    continue;
                }

                output.Append(working, position, match.Index - position);
                position = match.Index + match.Length;

                var name = match.Groups["name"].Value.ToLowerInvariant();

                if (DangerousElements.Contains(name))
                {
                    if (!isClosing && !isSelfClosing)
                    {
                        skipTag = name;
                        skipDepth = 1;
                    }
                    continue;
                }

                if (isClosing)
                {
                    AppendClosingTag(output, name);
                    continue;
                }

                AppendOpeningTag(output, name, match.Groups["attrs"].Value, isSelfClosing);
            }

            if (skipDepth == 0 && position <= working.Length)
                output.Append(working, position, working.Length - position);

            return output.ToString();
        }

        private static void AppendClosingTag(StringBuilder output, string name)
        {
            // Seuls les conteneurs de mise en forme sont réémis ; une </img> solitary
            // n'a pas lieu d'être et pourrait déséquilibrer le document du navigateur.
            if (HtmlVoidElements.Contains(name)) return;
            output.Append("</").Append(name).Append('>');
        }

        private static void AppendOpeningTag(StringBuilder output, string name, string rawAttrs, bool selfClose)
        {
            output.Append('<').Append(name);

            foreach (var (attrName, attrValue) in EnumerateAttributes(rawAttrs))
            {
                if (!IsAttributeAllowed(name, attrName)) continue;

                output.Append(' ').Append(attrName);

                if (attrValue == null) continue;

                var safeValue = SanitizeAttributeValue(name, attrName, attrValue);
                if (safeValue == null) continue;

                output.Append("=\"").Append(safeValue).Append('"');
            }

            // Les <br> « ouverts » (coupés par le regex) doivent rester équilibrés.
            if (!selfClose && !HtmlVoidElements.Contains(name))
                output.Append('>');
            else if (selfClose)
                output.Append(" />");
            else
                output.Append('>');
        }

        private static bool IsAttributeAllowed(string tag, string attribute)
        {
            if (attribute.StartsWith("on", StringComparison.OrdinalIgnoreCase)) return false;
            if (ForbiddenAttributes.Contains(attribute)) return false;

            return tag switch
            {
                "a" => attribute is "href" or "title" or "target" or "rel" or "name",
                "img" => attribute is "src" or "alt" or "title" or "width" or "height" or "class",
                _ => attribute is "style" or "class" or "title" or "dir" or "lang" or "id" or "align" or "colspan" or "rowspan" or "border" or "cellpadding" or "cellspacing" or "width" or "height" or "bgcolor" or "color" or "face" or "size"
            };
        }

        /// <summary>
        /// Neutralise les schémas d'URL dangereux. Un <c>javascript:</c> — y compris
        /// encodé en entités HTML ou avec des tabulations, que le navigateur tolère —
        /// ne doit jamais atteindre le <c>href</c>/<c>src</c> rendu.
        /// </summary>
        private static string? SanitizeAttributeValue(string tag, string attribute, string value)
        {
            // entity HTML décodées puis tabulations/retours/newlines supprimés : c'est
            // exactement ce que fait le navigateur avant d'évaluer une URL.
            var decoded = System.Net.WebUtility.HtmlDecode(value);
            var compact = Regex.Replace(decoded, @"[\s\x00-\x1F]+", "").ToLowerInvariant();

            if (attribute is "href" or "src")
            {
                // URL relatives : conservées telles quelles (elles sont inoffensives).
                if (!Regex.IsMatch(compact, @"^[a-z0-9.+-]*:") && !compact.StartsWith("//"))
                    return EscapeAttribute(decoded);

                var colon = compact.IndexOf(':');
                var scheme = colon > 0 ? compact[..colon] : "";

                // "data:" est exclu : il permet de charger un document arbitraire.
                if (!AllowedSchemes.Contains(scheme))
                    return null;

                return EscapeAttribute(decoded);
            }

            if (attribute == "style")
            {
                // On retire d'abord les fonctions qui chargent une ressource ou exécutent du
                // script, puis on applique une liste blanche de caractères.
                var style = Regex.Replace(value, @"url\s*\([^)]*\)", "", RegexOptions.IgnoreCase);
                style = Regex.Replace(style, @"expression\s*\([^)]*\)", "", RegexOptions.IgnoreCase);
                style = Regex.Replace(style, @"\b(behaviou?r|-moz-binding|@import)\b", "", RegexOptions.IgnoreCase);

                // Liste blanche : un email légitime n'a besoin que de ces caractères dans un
                // style inline. Conserver « < » ou « & » allowrait de reconstruire du balisage.
                style = Regex.Replace(style, @"[^a-zA-Z0-9#.,%()\-:;/ ']", "");
                style = Regex.Replace(style, @"\s{2,}", " ").Trim().Trim(';');

                return string.IsNullOrWhiteSpace(style) ? null : EscapeAttribute(style);
            }

            if (attribute == "target")
                return "_blank";

            return EscapeAttribute(value);
        }

        private static string EscapeAttribute(string value) =>
            value.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");

        /// <summary>Découpe une chaîne d'attributs en paires (nom, valeur), tolérante aux guillemets absents.</summary>
        private static IEnumerable<(string Name, string? Value)> EnumerateAttributes(string raw)
        {
            var i = 0;
            while (i < raw.Length)
            {
                while (i < raw.Length && (char.IsWhiteSpace(raw[i]) || raw[i] == '/')) i++;
                if (i >= raw.Length) yield break;

                var nameStart = i;
                while (i < raw.Length && !char.IsWhiteSpace(raw[i]) && raw[i] != '=') i++;
                var name = raw[nameStart..i];

                if (name.Length == 0) { i++; continue; }

                while (i < raw.Length && char.IsWhiteSpace(raw[i])) i++;

                if (i < raw.Length && raw[i] == '=')
                {
                    i++;
                    while (i < raw.Length && char.IsWhiteSpace(raw[i])) i++;

                    if (i < raw.Length && (raw[i] == '"' || raw[i] == '\''))
                    {
                        var quote = raw[i++];
                        var valueStart = i;
                        while (i < raw.Length && raw[i] != quote) i++;
                        yield return (name, raw[valueStart..i]);
                        if (i < raw.Length) i++;
                    }
                    else
                    {
                        var valueStart = i;
                        while (i < raw.Length && !char.IsWhiteSpace(raw[i])) i++;
                        yield return (name, raw[valueStart..i]);
                    }
                }
                else
                {
                    yield return (name, null);
                }
            }
        }

        /// <summary>
        /// Éléments sans contenu : leur balise fermante est interdite en HTML. Les
        /// réémettre produirait un document invalide que le navigateur répare — et cette
        /// « réparation » est justement le genre de divergence qu'on veut éviter.
        /// </summary>
        private static readonly HashSet<string> HtmlVoidElements = new(StringComparer.OrdinalIgnoreCase)
        {
            "area", "base", "br", "col", "embed", "hr", "img", "input",
            "link", "meta", "param", "source", "track", "wbr"
        };
    }
}
