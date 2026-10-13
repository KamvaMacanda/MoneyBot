namespace MoneyBot.Models
{
    public class Conversation
    { 
        public int Id { get; set;  }

        public string UserID { get; set; } = " ";

        public string Title { get; set; } = "New Chat";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool Archived { get; set; }


    }
}
