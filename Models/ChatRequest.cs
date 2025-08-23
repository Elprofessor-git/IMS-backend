namespace Backend_Gestion_Magasin_API.Models
{
    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? SessionId { get; set; }
    }
}

