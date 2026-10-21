namespace MoneyBot.DTOs
{ 

    // Incoming text from the user 
    public class ChatQueryDto
    {

        public string Message { get; set; } = string.Empty;
        public string? ConversationId { get; set; }

        public bool IsOfflineMode { get; set; }


    }
}
