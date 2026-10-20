namespace MoneyBot.DTOs
{ 

    // Incoming text from the user 
    public class ChatQuery
    {

        public string Message { get; set; } = string.Empty;
        public string? ConversationId { get; set; }


}
}
