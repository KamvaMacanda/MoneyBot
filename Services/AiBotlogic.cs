using Microsoft.EntityFrameworkCore;
using MoneyBot.Data;

namespace MoneyBot.Services
{
    /// <summary>
    /// Primary conversational orchestrator: uses Gemini AI when online and credits exist,
    /// and seamlessly delegates to ChatBotLogic (offline LINQ telemetry) when offline or quota is exhausted.
    /// </summary>
    public class AiBotLogic
    {
        private readonly AppDbContext _db;
        private readonly GeminiService _gemini;
        private readonly ChatBotLogic _offlineChatLogic;
        private readonly ILogger<AiBotLogic> _logger;

        public AiBotLogic(
            AppDbContext db,
            GeminiService gemini,
            ChatBotLogic offlineChatLogic,
            ILogger<AiBotLogic> logger)
        {
            _db = db;
            _gemini = gemini;
            _offlineChatLogic = offlineChatLogic;
            _logger = logger;
        }

        /// <summary>
        ///  uses Gemini AI if online and credits is available,
        /// otherwise executes ChatBotLogic's keyword & LINQ rules.
        /// </summary>
      
        public async Task<string> AskAsync(string userId, string message, bool forceOffline = false)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "Please enter a question or request.";

            // If user selected Offline Mode or Gemini credits are exhausted, use offline keyword logic
            if (forceOffline || _gemini.IsQuotaExceeded)
            {
                return await _offlineChatLogic.AskAsync(userId, message);
            }

            try
            {
                // Build financial ledger context using LINQ for Gemini
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var startOfMonth = new DateOnly(today.Year, today.Month, 1);

                var txns = await _db.Transactions
                    .Where(t => t.UserID == userId && t.Date >= startOfMonth)
                    .ToListAsync();

                var totalSpent = txns.Where(t => t.Amount > 0).Sum(t => t.Amount);
                var totalIncome = txns.Where(t => t.Amount < 0).Sum(t => -t.Amount);
                var topCategories = txns.Where(t => t.Amount > 0)
                    .GroupBy(t => t.Category ?? "Other")
                    .Select(g => $"{g.Key}: R{g.Sum(x => x.Amount):N2}")
                    .Take(4);

                var financialContext = $"Total Outflow this month: R{totalSpent:N2}. Total Inflow: R{totalIncome:N2}. " +
                                       $"Top Categories: {string.Join(", ", topCategories)}.";

                // Request answer from Gemini AI
                var aiReply = await _gemini.AskGeminiAsync(message, financialContext);
                if (!string.IsNullOrWhiteSpace(aiReply))
                {
                    return aiReply;
                }

                //  If Gemini failed, timed out, or ran out of credits, fallback to offline ChatBotLogic
                return await _offlineChatLogic.AskAsync(userId, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AiBotLogic. Falling back to offline keyword logic.");
                return await _offlineChatLogic.AskAsync(userId, message);
            }
        }
    }
}