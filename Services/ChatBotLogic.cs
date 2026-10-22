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
            // Simple validation
            if (string.IsNullOrWhiteSpace(message))
            {
                return "Please enter a question or request.";
            }
            // Clean & decrypt text if encrypted
            string cleanText = Cryptography.DecryptMessageIfEncrypted(message).Trim().ToLowerInvariant();


            // Date ranges for this month
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var startOfMonth = new DateOnly(today.Year, today.Month, 1);
            var startOfLastMonth = startOfMonth.AddMonths(-1);
            var endOfLastMonth = startOfMonth.AddDays(-1);


            // Fetch user transactions for this month (both Plaid and CSV)
            var currentTxns = await db.Transactions
                .Where(t => t.UserID == userId && t.Date >= startOfMonth && t.Date <= today)
                .ToListAsync();
            if (!currentTxns.Any())
            {
                return "No transactions found for this month yet. Please connect a bank via Plaid or upload a CSV statement.";
            } 

            // 1. Where did money go / Spending overview / Outflow / Categories
            if (cleanText.Contains("where") || cleanText.Contains("spend") || cleanText.Contains("outflow") || cleanText.Contains("categor"))
            {
                var totalSpent = currentTxns.Where(t => t.Amount > 0).Sum(t => t.Amount);
                var categories = currentTxns.Where(t => t.Amount > 0)
                    .GroupBy(t => t.Category ?? "uncategorized")
                    .Select(g => new { Category = g.Key, Total = g.Sum(x => x.Amount) })
                    .OrderByDescending(x => x.Total)
                    .ToList();
                var lines = categories.Select(c => $"• {c.Category}: R{c.Total:N2}");
                return $"Total outflow this month is R{totalSpent:N2}:\n" + string.Join("\n", lines);
            }
            // 2. Top Merchants / Stores / Places 

            if (cleanText.Contains("merchant") || cleanText.Contains("store") || cleanText.Contains("shop") || cleanText.Contains("top") || cleanText.Contains("most"))
            {
                var topMerchants = currentTxns.Where(t => t.Amount > 0)
                    .GroupBy(t => t.Merchant ?? "Unknown")
                    .Select(g => new { Merchant = g.Key, Total = g.Sum(x => x.Amount) })
                    .OrderByDescending(x => x.Total)
                    .Take(5)
                    .ToList();
                var lines = topMerchants.Select(m => $"• {m.Merchant}: R{m.Total:N2}");
                return "Your top merchants this month:\n" + string.Join("\n", lines);
            }
            // 3. Savings / Inflow / Income
            if (cleanText.Contains("save") || cleanText.Contains("saving") || cleanText.Contains("income") || cleanText.Contains("salary"))
            {
                var totalSpent = currentTxns.Where(t => t.Amount > 0).Sum(t => t.Amount);
                var totalIncome = currentTxns.Where(t => t.Amount < 0).Sum(t => -t.Amount);
                var saved = totalIncome - totalSpent;
                var rate = totalIncome > 0 ? (saved / totalIncome) * 100m : 0m;
                return $"Total Inflow: R{totalIncome:N2} | Total Outflow: R{totalSpent:N2}\n" +
                       $"Net Saved: R{saved:N2} ({rate:N1}% of income).";
            }
            // 4. Subscriptions / Recurring Bills
            if (cleanText.Contains("subscript") || cleanText.Contains("recur") || cleanText.Contains("bill") || cleanText.Contains("netflix") || cleanText.Contains("spotify"))
            {
                var lastMonthMerchants = await db.Transactions
                    .Where(t => t.UserID == userId && t.Date >= startOfLastMonth && t.Date <= endOfLastMonth && t.Amount > 0 && t.Merchant != null)
                    .Select(t => t.Merchant!)
                    .Distinct()
                    .ToListAsync();
                var recurring = currentTxns
                    .Where(t => t.Amount > 0 && t.Merchant != null && lastMonthMerchants.Contains(t.Merchant))
                    .GroupBy(t => t.Merchant!)
                    .Select(g => new { Merchant = g.Key, Amount = g.OrderByDescending(x => x.Date).First().Amount })
                    .ToList();
                if (!recurring.Any())
                {
                    return "No recurring monthly charges detected across the last two months.";
                }
                var lines = recurring.Select(r => $"• {r.Merchant}: R{r.Amount:N2}/mo");
                return "Recurring monthly charges:\n" + string.Join("\n", lines);
            }

            // 5. Biggest / Largest expense
            if (cleanText.Contains("big") || cleanText.Contains("large") || cleanText.Contains("max") || cleanText.Contains("highest"))
            {
                var biggest = currentTxns.Where(t => t.Amount > 0).OrderByDescending(t => t.Amount).FirstOrDefault();
                return biggest != null
                    ? $"Your largest expense this month was R{biggest.Amount:N2} at {biggest.Merchant} on {biggest.Date:dd MMM}."
                    : "No expense transactions found.";
            } 


            // 6. Recent / Latest transactions
            if (cleanText.Contains("recent") || cleanText.Contains("latest") || cleanText.Contains("last"))
            {
                var recent = currentTxns.OrderByDescending(t => t.Date).Take(5);
                var lines = recent.Select(t => $"• {t.Date:dd MMM}: {t.Merchant} (R{Math.Abs(t.Amount):N2})");
                return "Your 5 most recent transactions:\n" + string.Join("\n", lines);
            } 


            // 7. General summary if none of the above specific keywords matched
            var defaultTotal = currentTxns.Where(t => t.Amount > 0).Sum(t => t.Amount);
            return $"You have {currentTxns.Count} transaction(s) recorded this month with total outflow of R{defaultTotal:N2}. " +
                   $"Ask me about: your spending breakdown, top merchants, recurring subscriptions, or savings.";
        }


  






    }
}
