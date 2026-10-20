using MoneyBot.Data;

namespace MoneyBot.Services
{   
    
    
    /// <summary>
    /// queries PostgreSQL via LINQ for financial facts,
    /// and uses Gemini AI for conversational, advisory answers.
    /// </summary>
    public class AiBotlogic
    {
        private readonly AppDbContext _db;
        private readonly GeminiService _gemini;
        private readonly ILogger<ChatBotLogic> _logger;

        public AiBotlogic(AppDbContext db, GeminiService gemini, ILogger<ChatBotLogic> logger)
        {
            _db = db;
            _gemini = gemini;
            _logger = logger;
        }


        /// <summary>
        /// Entry point: gathers financial ledger context via LINQ, 
        /// checks Gemini AI, and falls back to  set LINQ answers.
        /// </summary>
        public async Task<string> AskAsync(string userId, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "Please enter a question or request.";
            try
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var startOfMonth = new DateOnly(today.Year, today.Month, 1);
                // 1. LINQ: Aggregate user transactions for context
                var txns = await _db.Transactions
                    .Where(t => t.UserID == userId && t.Date >= startOfMonth)
                    .ToListAsync();
                var totalSpent = txns.Where(t => t.Amount > 0).Sum(t => t.Amount);
                var totalIncome = txns.Where(t => t.Amount < 0).Sum(t => -t.Amount);
                var topCategories = txns.Where(t => t.Amount > 0)
                    .GroupBy(t => t.Category ?? "Other")
                    .Select(g => $"{g.Key}: R{g.Sum(x => x.Amount):N2}")
                    .Take(4);
                var financialContext = $"Total Outflow this month: R{totalSpent:N2}. Total Inflow: R{totalIncome:N2}. Top Categories: {string.Join(", ", topCategories)}.";
                // 2. Try Gemini AI conversation
                var aiReply = await _gemini.AskGeminiAsync(message, financialContext);
                if (!string.IsNullOrWhiteSpace(aiReply))
                {
                    return aiReply;
                }
                // 3. Fallback: Local LINQ keyword matching
                var text = message.ToLower();
                if (text.Contains("spend") || text.Contains("where") || text.Contains("category"))
                {
                    if (!txns.Any()) return "No transactions found for this month yet. Connect a bank or upload a CSV to see spending.";
                    return $"You've spent R{totalSpent:N2} so far this month:\n" + string.Join("\n", topCategories.Select(c => $"• {c}"));
                }
                if (text.Contains("save") || text.Contains("savings"))
                {
                    var saved = totalIncome - totalSpent;
                    return $"Income: R{totalIncome:N2} | Outflow: R{totalSpent:N2} | Net Saved: R{saved:N2}.";
                }
                return $"Total spending this month is R{totalSpent:N2} across {txns.Count} transaction(s). How else can I assist you?";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing financial query for user {UserId}", userId);
                return "An error occurred while analyzing your data. Please try again.";
            }
        }



    }
}
