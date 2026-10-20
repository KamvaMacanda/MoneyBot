namespace MoneyBot.DTOs
{
    public class ChatResponse
    {
        public string Reply { get; set; } = string.Empty;
        public bool IsAiGenerated { get; set; }
        public string? Error { get; set; }
    }
}
