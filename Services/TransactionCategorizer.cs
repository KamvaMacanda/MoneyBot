namespace MoneyBot.Services
{
    public static class TransactionCategorizer
    {
        private static readonly Dictionary<string, string> Rules = new()
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
            if (string.IsNullOrWhiteSpace(description))
            {
                return "uncategorized";
            }

            foreach (var rule in Rules)
            {
                if (description.Contains(rule.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return rule.Value;
                }
            }

            return "uncategorized";
        }
    }
}
