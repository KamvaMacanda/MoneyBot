namespace MoneyBot.Services.CSV
{
    public class TransactionCategorizer
    { 

        //Use .CONTAINS 
        private static readonly Dictionary<string, string> Rules = new(StringComparer.OrdinalIgnoreCase)
        {
            ["WOOLWORTHS"] = "groceries",
            ["CHECKERS"] = "groceries",
            ["PICK N PAY"] = "groceries",
            ["SPAR"] = "groceries",
            ["UBER"] = "transport",
            ["BOLT"] = "transport",
            ["NETFLIX"] = "subscriptions",
            ["SPOTIFY"] = "subscriptions",
            ["SHOWMAX"] = "subscriptions",
        };

        public static string Categorize(string description)
        {
            foreach (var (keyword, category) in Rules)
                if (description.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return category;

            return "uncategorized";
        }
    }
}
