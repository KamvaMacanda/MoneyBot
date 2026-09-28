namespace MoneyBot.Models
{
    public class Item
    { 

        public int Id { get; set; } 

        public string UserID { get; set;  } = "";
        public  string PlaidAccessToken {  get; set; } = "";

        public string? Cursor { get; set;  } = "";
        public string BankName { get; set; } = "";



    }
}
