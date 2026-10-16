using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MoneyBot.Data;
using MoneyBot.Models;
using MoneyBot.Services;
using System.Security.Claims;

namespace MoneyBot.Views
{
    [IgnoreAntiforgeryToken(Order = 1001)]
    public class IndexModel : PageModel
    {
        private readonly ChatBotLogic _chatLogic;
        private readonly PlaidService _plaidService;
        private readonly AppDbContext _db;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            ChatBotLogic chatLogic, 
            PlaidService plaidService, 
            AppDbContext db, 
            ILogger<IndexModel> logger)
        {
            _chatLogic = chatLogic;
            _plaidService = plaidService;
            _db = db;
            _logger = logger;
        }

        // Live balances and metrics directly from Plaid connection
        public decimal TotalBalance { get; set; } = 0m;
        public decimal RandBalance { get; set; } = 0m;
        public decimal MonthlyOutflow { get; set; }
        public decimal MonthlyInflow { get; set; }
        public decimal SavingsRate { get; set; }
        public int ActiveSubscriptionsCount { get; set; }
        public decimal SubscriptionsMonthlyTotal { get; set; }
        public decimal OutflowChangePercent { get; set; }

        public List<AccountDetail> ConnectedAccounts { get; set; } = new();

        public async Task OnGetAsync()
        {
            var userId = GetUserId();

            // 1. Fetch real, live account balances directly from Plaid API converted to ZAR
            try
            {
                ConnectedAccounts = await _plaidService.GetLiveAccountBalancesAsync(userId);
                TotalBalance = ConnectedAccounts.Sum(a => a.RandBalance);
                RandBalance = TotalBalance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load live Plaid balances for user {UserId}", userId);
            }

            // 2. Compute dynamic telemetry directly from synced Plaid transactions
            try
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var startOfMonth = new DateOnly(today.Year, today.Month, 1);
                var startOfLastMonth = startOfMonth.AddMonths(-1);
                var endOfLastMonth = startOfMonth.AddDays(-1);

                // All transactions for this user
                var userTxns = await _db.Transactions
                    .Where(t => t.UserID == userId)
                    .ToListAsync();

                var thisMonthTxns = userTxns
                    .Where(t => t.Date >= startOfMonth && t.Date <= today)
                    .ToList();

                var lastMonthTxns = userTxns
                    .Where(t => t.Date >= startOfLastMonth && t.Date <= endOfLastMonth)
                    .ToList();

                if (thisMonthTxns.Any())
                {
                    // Plaid amounts: positive amount is expenditure/outflow, negative is inflow/credit
                    MonthlyOutflow = thisMonthTxns.Where(t => t.Amount > 0).Sum(t => t.Amount);
                    MonthlyInflow = thisMonthTxns.Where(t => t.Amount < 0).Sum(t => -t.Amount);

                    if (MonthlyInflow > 0)
                    {
                        var saved = MonthlyInflow - MonthlyOutflow;
                        SavingsRate = Math.Max(0, (saved / MonthlyInflow) * 100m);
                    }
                    else if (MonthlyInflow == 0 && TotalBalance > 0)
                    {
                        // Fallback savings rate ratio if income occurs outside current window
                        SavingsRate = Math.Max(0, Math.Min(100m, (TotalBalance / (TotalBalance + MonthlyOutflow)) * 100m));
                    }

                    // Calculate month-over-month outflow change
                    var lastMonthOutflow = lastMonthTxns.Where(t => t.Amount > 0).Sum(t => t.Amount);
                    if (lastMonthOutflow > 0)
                    {
                        OutflowChangePercent = ((MonthlyOutflow - lastMonthOutflow) / lastMonthOutflow) * 100m;
                    }
                }

                // Detect recurring subscriptions from transactions
                var subscriptionTxns = userTxns
                    .Where(t => (t.Category != null && t.Category.Contains("subscription", StringComparison.OrdinalIgnoreCase)) ||
                                (t.Merchant != null && (t.Merchant.Contains("netflix", StringComparison.OrdinalIgnoreCase) ||
                                                        t.Merchant.Contains("spotify", StringComparison.OrdinalIgnoreCase) ||
                                                        t.Merchant.Contains("rain", StringComparison.OrdinalIgnoreCase) ||
                                                        t.Merchant.Contains("apple", StringComparison.OrdinalIgnoreCase))))
                    .GroupBy(t => t.Merchant)
                    .ToList();

                ActiveSubscriptionsCount = subscriptionTxns.Count;
                SubscriptionsMonthlyTotal = subscriptionTxns.Sum(g => g.OrderByDescending(x => x.Date).First().Amount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compute real dashboard metrics on Index OnGet");
            }
        }

        public async Task<IActionResult> OnPostAskAsync([FromBody] ChatMessageRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return new JsonResult(new { error = "Message cannot be empty." }) { StatusCode = 400 };
            }

            try
            {
                var userId = GetUserId();
                var reply = await _chatLogic.AskAsync(userId, request.Message);
                return new JsonResult(new { reply });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat message in IndexModel.OnPostAsk");
                return new JsonResult(new { error = "Failed to process chat query." }) { StatusCode = 500 };
            }
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "default-user";
        }

        public record ChatMessageRequest(string Message, string? ConversationId);
    }
}
