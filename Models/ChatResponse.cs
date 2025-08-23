namespace Backend_Gestion_Magasin_API.Models
{
    public class ChatResponse
    {
        public string Response { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool Success { get; set; } = true;
        public string? Error { get; set; }
    }
}

