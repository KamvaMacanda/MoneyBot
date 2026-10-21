namespace MoneyBot.Models
{
    public class Dashboard
    {
        /// <summary>
        /// Encapsulates live financial metrics, balances, and connected accounts.
        /// </summary>
        public decimal TotalBalance { get; set; } = 0m;
        public decimal RandBalance { get; set; } = 0m;
        public decimal MonthlyOutflow { get; set; }
        public decimal MonthlyInflow { get; set; }
        public decimal SavingsRate { get; set; }
        public int ActiveSubscriptionsCount { get; set; }
        public decimal SubscriptionsMonthlyTotal { get; set; }
        public decimal OutflowChangePercent { get; set; }
        // Live connected accounts fetched via Plaid
        public List<AccountDetail> ConnectedAccounts { get; set; } = new();
    }
}
