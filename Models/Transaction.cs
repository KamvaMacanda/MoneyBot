namespace MoneyBot.Models
{
    public class Transaction
    { 
        public string TransactionId { get; set; } = "";
        public string UserID { get; set; } = "";
        public DateOnly Date { get; set; } 
        public decimal Amount { get; set; }
        public string? Merchant { get; set; }
        public string Description { get; set; } = "";
        public string? Category { get; set; } 
        public bool Pending { get; set; }
        public string Source { get; set; } = "";
        public bool IsCategoryManual { get; set; }
    }
}
