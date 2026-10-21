using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MoneyBot.Data; 
using MoneyBot.Services; 

namespace MoneyBot.Services
{


    public class ChatBotLogic
    {
        private readonly AppDbContext db;

        private static readonly string[] KnownCategories =
        {
            "groceries", "transport", "dining", "shopping",
            "entertainment", "subscriptions", "utilities", "health",
            "medical", "travel", "education"
        };

        public ChatBotLogic(AppDbContext db)
        {
            this.db = db;
        }

        public async Task<string> AskAsync(string userId, string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                    return "Please enter a valid message or question.";

                //  Call static method on Cryptography type directly, not an instance
                string decryptedMessage = Cryptography.DecryptMessageIfEncrypted(message);
                var text = decryptedMessage.ToLower().Trim();
                var (start, end) = GetThisMonthRange();
                var hasNumber = Regex.IsMatch(text, @"\d+");

                // 1. Savings target: "how can I save 300"
                if (hasNumber && ContainsAny(text, "save", "saving") && ContainsAny(text, "target", "can i", "how can", "how to"))
                    return await HandleSavingsTargetAsync(userId, text, start, end);

                // 2. Savings rate / general savings: "how much have I saved", "savings rate"
                if (ContainsAny(text, "save", "saved", "savings"))
                    return await HandleSavingsRateAsync(userId, start, end);

                // 3. Subscriptions / recurring bills
                if (ContainsAny(text, "recurring", "subscription", "subscriptions", "bills", "membership"))
                    return await HandleRecurringChargesAsync(userId, start, end);

                // 4. Comparison to previous month
                if (ContainsAny(text, "compare", "last month", "versus", " vs ", "trend", "difference"))
                    return await HandleCompareToLastMonthAsync(userId, start, end);

                // 5. Biggest / smallest expense
                if (ContainsAny(text, "biggest", "largest", "most expensive", "highest"))
                    return await HandleBiggestTransactionAsync(userId, start, end);

                if (ContainsAny(text, "smallest", "cheapest", "lowest"))
                    return await HandleSmallestTransactionAsync(userId, start, end);

                // 6. Recent transactions
                if (ContainsAny(text, "recent", "latest", "last transaction", "last purchase"))
                    return await HandleRecentTransactionsAsync(userId);

                // 7. Income inquiries
                if (ContainsAny(text, "income", "salary", "earnings", "inflow", "deposit"))
                    return await HandleIncomeAsync(userId, start, end);

                // 8. Pending transactions
                if (ContainsAny(text, "pending", "unposted", "clearing"))
                    return await HandlePendingTransactionsAsync(userId);

                // 9. Specific category: "how much did I spend on groceries"
                var matchedCategory = KnownCategories.FirstOrDefault(c => text.Contains(c));
                if (matchedCategory != null)
                    return await HandleCategorySpecificAsync(userId, matchedCategory, start, end);

                // 10. Top merchants
                if (ContainsAny(text, "most", "top", "store", "shops", "merchant", "merchants"))
                    return await HandleTopMerchantsAsync(userId, start, end);

                // 11. General spending by category
                if (ContainsAny(text, "where", "spend", "spending", "category", "categories", "breakdown", "overview"))
                    return await HandleSpendingByCategoryAsync(userId, start, end);

                // 12. Transaction count / summary
                if (ContainsAny(text, "count", "how many", "total transactions"))
                    return await HandleTransactionCountAsync(userId, start, end);

                // 13.  response
                return "I can help with:\n" +
                       "• Where your money went (category breakdown)\n" +
                       "• Specific category spend (e.g. groceries, dining, transport, subscriptions)\n" +
                       "• Top merchants & stores\n" +
                       "• Recurring subscriptions & monthly bills\n" +
                       "• Recent or pending transactions\n" +
                       "• Comparing this month vs last month\n" +
                       "• Your biggest and smallest expense\n" +
                       "• Your income and savings rate\n" +
                       "• A savings target like \"how can I save 100\"";
            }
            catch (Exception ex)
            {
                return $"An error occurred while analyzing your financial data: {ex.Message}";
            }
        }

     

        private static bool ContainsAny(string text, params string[] keywords) =>
            keywords.Any(text.Contains);

        private (DateOnly start, DateOnly end) GetThisMonthRange()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return (new DateOnly(today.Year, today.Month, 1), today);
        }

        private (DateOnly start, DateOnly end) GetLastMonthRange()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var firstOfThisMonth = new DateOnly(today.Year, today.Month, 1);
            var lastMonthEnd = firstOfThisMonth.AddDays(-1);
            var lastMonthStart = new DateOnly(lastMonthEnd.Year, lastMonthEnd.Month, 1);
            return (lastMonthStart, lastMonthEnd);
        }

        /// "Where did my money go" / spending broken down by category
        private async Task<string> HandleSpendingByCategoryAsync(string userId, DateOnly start, DateOnly end)
        {
            var results = await db.Transactions
                .Where(t => t.UserID == userId && t.Date >= start && t.Date <= end && t.Amount > 0)
                .GroupBy(t => t.Category ?? "uncategorized")
                .Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) })
                .OrderByDescending(x => x.Total)
                .ToListAsync();

            if (results.Count == 0)
                return "No spending transactions found for this month yet.";

            var total = results.Sum(r => r.Total);
            var lines = results.Select(r => $"• {r.Category}: R{r.Total:N2}");
            return $"You've spent R{total:N2} so far this month:\n" + string.Join("\n", lines);
        }

        /// <summary>Top 5 merchants by spend this month.</summary>
        private async Task<string> HandleTopMerchantsAsync(string userId, DateOnly start, DateOnly end)
        {
            var results = await db.Transactions
                .Where(t => t.UserID == userId && t.Date >= start && t.Date <= end && t.Amount > 0)
                .GroupBy(t => t.Merchant ?? "Unknown")
                .Select(g => new { Merchant = g.Key, Total = g.Sum(t => t.Amount) })
                .OrderByDescending(x => x.Total)
                .Take(5)
                .ToListAsync();

            if (results.Count == 0)
                return "No merchant transactions found for this month yet.";

            var lines = results.Select(r => $"• {r.Merchant}: R{r.Total:N2}");
            return "Your top merchants this month:\n" + string.Join("\n", lines);
        }

        /// <summary>"How much did I spend on [category]" — totals for one named category.</summary>
        private async Task<string> HandleCategorySpecificAsync(string userId, string category, DateOnly start, DateOnly end)
        {
            var total = await db.Transactions
                .Where(t => t.UserID == userId && t.Date >= start && t.Date <= end
                            && t.Amount > 0 && (t.Category != null && t.Category.ToLower().Contains(category)))
                .SumAsync(t => t.Amount);

            if (total == 0)
                return $"No {category} spending found this month.";

            return $"You've spent R{total:N2} on {category} this month.";
        }

        /// <summary>Recurring charges across this month and last month.</summary>
        private async Task<string> HandleRecurringChargesAsync(string userId, DateOnly start, DateOnly end)
        {
            var (lastStart, lastEnd) = GetLastMonthRange();

            var thisMonthMerchants = await db.Transactions
                .Where(t => t.UserID == userId && t.Date >= start && t.Date <= end && t.Amount > 0 && t.Merchant != null)
                .Select(t => t.Merchant!)
                .Distinct()
                .ToListAsync();

            var lastMonthMerchants = await db.Transactions
                .Where(t => t.UserID == userId && t.Date >= lastStart && t.Date <= lastEnd && t.Amount > 0 && t.Merchant != null)
                .Select(t => t.Merchant!)
                .Distinct()
                .ToListAsync();

            var recurring = thisMonthMerchants.Intersect(lastMonthMerchants).ToList();

            if (recurring.Count == 0)
                return "No recurring charges detected across the last two months.";

            var totals = await db.Transactions
                .Where(t => t.UserID == userId && t.Date >= start && t.Date <= end
                            && t.Amount > 0 && recurring.Contains(t.Merchant!))
                .GroupBy(t => t.Merchant!)
                .Select(g => new { Merchant = g.Key, Total = g.Sum(t => t.Amount) })
                .ToListAsync();

            var lines = totals.Select(t => $"• {t.Merchant}: R{t.Total:N2}/mo");
            return "These charges appear both this month and last month:\n" + string.Join("\n", lines);
        }

        /// <summary>Comparing this month total vs last month total.</summary>
        private async Task<string> HandleCompareToLastMonthAsync(string userId, DateOnly start, DateOnly end)
        {
            var (lastStart, lastEnd) = GetLastMonthRange();

            var thisMonthTotal = await db.Transactions
                .Where(t => t.UserID == userId && t.Date >= start && t.Date <= end && t.Amount > 0)
                .SumAsync(t => t.Amount);

            var lastMonthTotal = await db.Transactions
                .Where(t => t.UserID == userId && t.Date >= lastStart && t.Date <= lastEnd && t.Amount > 0)
                .SumAsync(t => t.Amount);

            if (lastMonthTotal == 0)
                return $"You've spent R{thisMonthTotal:N2} so far this month. No data from last month to compare.";

            var diff = thisMonthTotal - lastMonthTotal;
            var direction = diff > 0 ? "more" : "less";

            return $"This month: R{thisMonthTotal:N2}. Last month: R{lastMonthTotal:N2}.\n" +
                   $"You've spent R{Math.Abs(diff):N2} {direction} than last month.";
        }

        /// <summary>Single largest expense this month.</summary>
        private async Task<string> HandleBiggestTransactionAsync(string userId, DateOnly start, DateOnly end)
        {
            var biggest = await db.Transactions
                .Where(t => t.UserID == userId && t.Date >= start && t.Date <= end && t.Amount > 0)
                .OrderByDescending(t => t.Amount)
                .FirstOrDefaultAsync();

            if (biggest == null)
                return "No expense transactions found for this month yet.";

            return $"Your biggest expense this month was R{biggest.Amount:N2} at {biggest.Merchant ?? "Unknown"} on {biggest.Date:dd MMM}.";
        }

        /// <summary>Single smallest expense this month.</summary>
        private async Task<string> HandleSmallestTransactionAsync(string userId, DateOnly start, DateOnly end)
        {
            var smallest = await db.Transactions
                .Where(t => t.UserID == userId && t.Date >= start && t.Date <= end && t.Amount > 0)
                .OrderBy(t => t.Amount)
                .FirstOrDefaultAsync();

            if (smallest == null)
                return "No expense transactions found for this month yet.";

            return $"Your smallest expense this month was R{smallest.Amount:N2} at {smallest.Merchant ?? "Unknown"} on {smallest.Date:dd MMM}.";
        }

        /// <summary>Latest transactions.</summary>
        private async Task<string> HandleRecentTransactionsAsync(string userId)
        {
            var recent = await db.Transactions
                .Where(t => t.UserID == userId)
                .OrderByDescending(t => t.Date)
                .Take(5)
                .ToListAsync();

            if (recent.Count == 0)
                return "No transactions found on your account.";

            var lines = recent.Select(t => $"• {t.Date:dd MMM} - {t.Merchant ?? "Transaction"}: R{Math.Abs(t.Amount):N2} ({(t.Amount > 0 ? "outflow" : "inflow")})");
            return "Here are your 5 most recent transactions:\n" + string.Join("\n", lines);
        }

        /// <summary>Income this month.</summary>
        private async Task<string> HandleIncomeAsync(string userId, DateOnly start, DateOnly end)
        {
            var income = await db.Transactions
                .Where(t => t.UserID == userId && t.Date >= start && t.Date <= end && t.Amount < 0)
                .SumAsync(t => -t.Amount);

            if (income == 0)
                return "No income or salary deposits detected for this month yet.";

            return $"You have received R{income:N2} in total income/inflow this month.";
        }

        /// <summary>Pending transactions.</summary>
        private async Task<string> HandlePendingTransactionsAsync(string userId)
        {
            var pending = await db.Transactions
                .Where(t => t.UserID == userId && t.Pending)
                .OrderByDescending(t => t.Date)
                .ToListAsync();

            if (pending.Count == 0)
                return "You have no pending transactions currently clearing.";

            var lines = pending.Select(t => $"• {t.Date:dd MMM} - {t.Merchant ?? "Pending"}: R{t.Amount:N2}");
            return $"You have {pending.Count} pending transaction(s):\n" + string.Join("\n", lines);
        }

        /// <summary>Count of transactions.</summary>
        private async Task<string> HandleTransactionCountAsync(string userId, DateOnly start, DateOnly end)
        {
            var count = await db.Transactions
                .Where(t => t.UserID == userId && t.Date >= start && t.Date <= end)
                .CountAsync();

            return $"You have {count} recorded transactions so far this month.";
        }

        /// <summary>Income minus spending this month.</summary>
        private async Task<string> HandleSavingsRateAsync(string userId, DateOnly start, DateOnly end)
        {
            var income = await db.Transactions
                .Where(t => t.UserID == userId && t.Date >= start && t.Date <= end && t.Amount < 0)
                .SumAsync(t => -t.Amount);

            var spending = await db.Transactions
                .Where(t => t.UserID == userId && t.Date >= start && t.Date <= end && t.Amount > 0)
                .SumAsync(t => t.Amount);

            if (income == 0)
                return "No income detected this month to calculate a savings rate.";

            var saved = income - spending;
            var rate = saved / income * 100;

            return $"Income: R{income:N2}. Spending: R{spending:N2}.\n" +
                   $"You've saved R{saved:N2} this month ({rate:N1}% of income).";
        }

        /// <summary>"How can I save 300" — proposes cuts from the biggest categories.</summary>
        private async Task<string> HandleSavingsTargetAsync(string userId, string text, DateOnly start, DateOnly end)
        {
            var match = Regex.Match(text, @"\d+(\.\d+)?");

            if (!match.Success)
                return "How much would you like to save? Try \"how can I save 300\".";

            var target = decimal.Parse(match.Value);

            var categories = await db.Transactions
                .Where(t => t.UserID == userId && 
                t.Date >= start &&
                 t.Date <= end &&
                 t.Amount > 0)
                .GroupBy(t => t.Category ?? "other")
                .Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) })
                .OrderByDescending(x => x.Total)
                .ToListAsync();

            var suggestions = new List<string>();
            decimal remaining = target;

            foreach (var c in categories)
            {
                if (remaining <= 0) break;

                var cut = Math.Min(c.Total * 0.3m, remaining);
                if (cut < 10) continue;

                suggestions.Add($"• {c.Category}: cut R{cut:N2}");
                remaining -= cut;
            }

            if (suggestions.Count == 0)
                return "Not enough spending data this month to suggest cuts.";

            var reply = $"Here's one way to save R{target:N2}:\n" + string.Join("\n", suggestions);

            if (remaining > 0)
                reply += $"\n\nThat covers R{target - remaining:N2} — you'd need to find the remaining R{remaining:N2} elsewhere.";

            return reply;
        }


        /// <summary>
        /// Entry point: gathers financial ledger context via LINQ, checks Gemini AI, and falls back to deterministic LINQ answers.
        /// </summary>








    }
}
