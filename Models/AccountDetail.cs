namespace MoneyBot.Models
{

    /// <summary>
    ///  Represents a live bank account model fetched directly from Plaid
    /// </summary>
    public class AccountDetail
    {

        public string AccountId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Mask { get; set; } = string.Empty;
        public string Institution { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public string Currency { get; set; } = "RSA";
        public bool IsPositive { get; set; }
        public bool IsNegative { get; set; }
    }
}
