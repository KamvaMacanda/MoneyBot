using Microsoft.EntityFrameworkCore;
using MoneyBot.Data;

namespace MoneyBot.Services
{
    /// <summary>
    /// Gathers facts from PostgreSQL via LINQ, feeds them to Gemini AI as a system prompt to
    /// generate conversational savings insights, and falls back to ChatBotLogic if offline.
    /// </summary>
    public class AiBotLogic
    {
        private readonly AppDbContext _db;
        private readonly GeminiService _gemini;
        private readonly ChatBotLogic _offlineLogic;
        private readonly ILogger<AiBotLogic> _logger;

        public AiBotLogic(
            AppDbContext db,
            GeminiService gemini,
            ChatBotLogic offlineLogic,
            ILogger<AiBotLogic> logger)
        {
            _db = db;
            _gemini = gemini;
            _offlineLogic = offlineLogic;
            _logger = logger;
        }

        public async Task<string> AskAsync(string userId, string message, bool forceOffline = false)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "Please enter a question or request.";
            }

            // If offline mode is toggled or quota is finished, use direct LINQ logic
            if (forceOffline || _gemini.IsQuotaExceeded)
            {
                return await _offlineLogic.AskAsync(userId, message);
            }

            try
            {
                // Gather all financial context via LINQ (both Plaid and CSV records)
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var startOfMonth = new DateOnly(today.Year, today.Month, 1);

                var txns = await _db.Transactions
                    .Where(t => t.UserID == userId && t.Date >= startOfMonth && t.Date <= today)
                    .ToListAsync();

                var totalSpent = txns.Where(t => t.Amount > 0).Sum(t => t.Amount);
                var totalIncome = txns.Where(t => t.Amount < 0).Sum(t => -t.Amount);
                var topCategories = txns.Where(t => t.Amount > 0)
                    .GroupBy(t => t.Category ?? "Other")
                    .Select(g => $"{g.Key}: R{g.Sum(x => x.Amount):N2}")
                    .Take(4);
                var topMerchants = txns.Where(t => t.Amount > 0)
                    .GroupBy(t => t.Merchant ?? "Unknown")
                    .Select(g => $"{g.Key}: R{g.Sum(x => x.Amount):N2}")
                    .Take(3);

                var financialContext = $"Currency: South African Rand (ZAR).\n" +
                                       $"Total Outflow this month: R{totalSpent:N2}.\n" +
                                       $"Total Inflow/Income: R{totalIncome:N2}.\n" +
                                       $"Top Spending Categories: {string.Join(", ", topCategories)}.\n" +
                                       $"Top Merchants: {string.Join(", ", topMerchants)}.\n" +
                                       $"Transaction Count: {txns.Count}.\n";

                //  Ask Gemini to provide personalized answers and actionable savings insights
                var aiReply = await _gemini.AskGeminiAsync(message, financialContext);
                if (!string.IsNullOrWhiteSpace(aiReply))
                {
                    return aiReply;
                }

                // Fallback to LINQ keyword matching if Gemini call is empty
                return await _offlineLogic.AskAsync(userId, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing AI query. Falling back to offline LINQ logic.");
                return await _offlineLogic.AskAsync(userId, message);
            }
        }
    }
}