namespace MoneyBot.Services.CSV
{
    public class TransactionCategorizer
    { 

   //Manually import data with keyword and category 
        private static readonly Dictionary<string, string> Rules = new Dictionary< string, string>
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
