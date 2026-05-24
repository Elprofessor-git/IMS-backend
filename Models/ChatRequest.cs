namespace Backend_Gestion_Magasin_API.Models
{
    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? SessionId { get; set; }
        public List<ConversationMessage>? History { get; set; }
    }

    public class ConversationMessage
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
    }
}

