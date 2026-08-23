using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Tools;
using System.Text.Json.Nodes;

namespace Backend_Gestion_Magasin_API.Services
{
    public class ChatbotAgentService
    {
        private readonly GroqService  _groq;
        private readonly ToolExecutor _executor;
        private readonly ILogger<ChatbotAgentService> _logger;

        private const int MaxTurns = 50;

        private const string SystemPrompt = """
            Tu es un assistant IA intégré dans un système IMS (Inventory Management System)
            d'un atelier textile tunisien. Tes règles absolues :
            - Tu réponds TOUJOURS en français, même si la question est posée dans une autre langue.
            - Tu es en LECTURE SEULE : tu consultes les données, tu ne crées, modifies ou supprimes RIEN.
              Si l'utilisateur demande une action d'écriture, décline poliment en expliquant que tu es en lecture seule.
            - Tu utilises les outils disponibles pour accéder aux données réelles — jamais tu n'inventes de chiffres.
            - Si une donnée n'existe pas dans le système, dis-le clairement plutôt que d'inventer.
            - Quand l'utilisateur cite un NOM (fournisseur, client ou plateforme comme "dandy's"),
              filtre par nom en utilisant les paramètres texte des outils (ex : fournisseurNom, plateformeNom,
              clientNom) — jamais par un ID.
            - Sois concis et précis. Pour les listes, utilise des puces ou des tableaux lisibles.
            - Quand tu présentes des quantités, précise toujours l'unité.

            CARTE DU DOMAINE (structure des données) :
            - Plateforme (place de marché, ex : dandy's) → possède des Clients.
            - Client → rattaché à UNE plateforme.
            - CommandeClient → appartient à un Client → donc à une plateforme.
            - Achat → a un Fournisseur ; optionnellement lié à une CommandeClient ; contient des LignesAchat (articles).
              La plateforme d'un achat peut venir des LIGNES (typeDestination=Plateforme) OU de la COMMANDE liée
              (commandeClient.client.plateforme). get_achats couvre les deux.
            - Importation → a un Fournisseur ; contient des LignesImportation (articles), mode d'expédition, statuts.
            - Article → stock (Stock) et mouvements (MouvementStock). Stock types : Libre/Reserve/Importe.
            - Statuts achats : Brouillon/Soumis/Confirme/Livre/Annule. Statuts importations : Brouillon/Soumise/Validee/Recue/Annulee.
            - En cas de doute sur l'entité ou les relations concernées, appelle get_schema(sujet=...) pour obtenir le détail.
            """;

        public ChatbotAgentService(GroqService groq, ToolExecutor executor,
            ILogger<ChatbotAgentService> logger)
        {
            _groq     = groq;
            _executor = executor;
            _logger   = logger;
        }

        public string CurrentModel => _groq.Model;

        public Task<bool> IsAvailableAsync() => _groq.IsAvailableAsync();

        public async Task<ChatResponse> SendMessageAsync(ChatRequest request)
        {
            var messages = new List<object>
            {
                new { role = "system", content = SystemPrompt }
            };

            if (request.History is { Count: > 0 })
                foreach (var msg in request.History)
                    messages.Add(new { role = msg.Role, content = msg.Content });

            messages.Add(new { role = "user", content = request.Message });

            for (int turn = 0; turn < MaxTurns; turn++)
            {
                JsonNode groqResponse;
                try
                {
                    groqResponse = await _groq.CompleteAsync(messages, ImsTools.All);
                }
                catch (HttpRequestException ex)
                {
                    // 400 "tool_use_failed" : Groq a rejeté l'appel d'outil car un paramètre
                    // ne respectait pas le schéma (ex : texte dans un champ entier). Plutôt que
                    // de faire échouer tout l'échange, on guide le modèle et on relance le tour.
                    if (ex.Message.Contains("tool_use_failed", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning(
                            "Groq a rejeté un appel d'outil (tool_use_failed) au tour {Turn} : {Error}",
                            turn, ex.Message);
                        messages.Add(new
                        {
                            role = "system",
                            content =
                                "Ton appel d'outil précédent a été rejeté car un paramètre avait un " +
                                "type invalide (un ID entier a reçu un texte). Si l'utilisateur donne un " +
                                "NOM (fournisseur, client ou plateforme), utilise le paramètre " +
                                "texte dédié (fournisseurNom, plateformeNom, clientNom) au lieu " +
                                "d'un ID. Relance l'outil avec les bons paramètres, ou réponds poliment que " +
                                "tu ne trouves pas de données correspondantes."
                        });
                        continue;
                    }

                    _logger.LogError(ex, "Erreur Groq au tour {Turn}", turn);
                    return Failure("Le service IA est temporairement indisponible.", request.SessionId);
                }

                var choice       = groqResponse["choices"]?[0];
                var message      = choice?["message"];
                var finishReason = choice?["finish_reason"]?.GetValue<string>();
                var toolCalls    = message?["tool_calls"]?.AsArray();

                // Réponse texte finale
                if (finishReason == "stop" || toolCalls is null or { Count: 0 })
                {
                    var text = message?["content"]?.GetValue<string>() ?? "";
                    return new ChatResponse
                    {
                        Response  = text,
                        SessionId = request.SessionId,
                        Success   = true,
                        Timestamp = DateTime.UtcNow
                    };
                }

                // Ajout du message assistant (avec ses tool_calls) dans l'historique
                messages.Add(new
                {
                    role       = "assistant",
                    content    = message?["content"]?.GetValue<string>() ?? "",
                    tool_calls = toolCalls.Select(tc => new
                    {
                        id       = tc!["id"]!.GetValue<string>(),
                        type     = "function",
                        function = new
                        {
                            name      = tc["function"]!["name"]!.GetValue<string>(),
                            arguments = tc["function"]!["arguments"]!.GetValue<string>()
                        }
                    }).ToArray()
                });

                // Exécution de chaque outil et injection des résultats
                foreach (var tc in toolCalls)
                {
                    try
                    {
                        var toolId   = tc?["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N");
                        var toolName = tc?["function"]?["name"]?.GetValue<string>() ?? "inconnu";
                        var rawArgs  = tc?["function"]?["arguments"]?.GetValue<string>();
                        var toolArgs = string.IsNullOrWhiteSpace(rawArgs)
                            ? new JsonObject()
                            : JsonNode.Parse(rawArgs) as JsonObject ?? new JsonObject();

                        var result = await _executor.ExecuteAsync(toolName, toolArgs, request.UserId);

                        messages.Add(new
                        {
                            role         = "tool",
                            tool_call_id = toolId,
                            content      = result
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Exécution d'un outil du chatbot ignorée (tour {Turn}).", turn);
                    }
                }
            }

            _logger.LogWarning("Boucle agent : limite de {Max} tours atteinte pour session {Session}.",
                MaxTurns, request.SessionId);
            return Failure(
                "Le traitement a nécessité trop d'itérations. Reformulez votre question.",
                request.SessionId);
        }

        private static ChatResponse Failure(string msg, string? sessionId) => new()
        {
            Response  = msg,
            SessionId = sessionId,
            Success   = false,
            Error     = msg,
            Timestamp = DateTime.UtcNow
        };
    }
}
