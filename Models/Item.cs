namespace MoneyBot.Models
{
    public class Item
    { 

        public int Id { get; set; } 

        public string UserId { get; set;  } = "";
        public string PlaidAccessTokenEnc { get; set; } = "";

        public string? Cursor { get; set; } 
        public string BankName { get; set; } = "";



    }
}
