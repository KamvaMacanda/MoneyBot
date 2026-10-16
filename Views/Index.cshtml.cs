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

        // Real bank balances 
        public decimal TotalBalance { get; set; } = 0m;
        public decimal RandBalance { get; set; } = 0m;
        public decimal MonthlyOutflow { get; set; }
        public decimal MonthlyInflow { get; set; }
        public decimal SavingsRate { get; set; }
        public int ActiveSubscriptionsCount { get; set; }
        public decimal SubscriptionsMonthlyTotal { get; set; }

        public List<AccountDetail> ConnectedAccounts { get; set; } = new();

        // Contacts Plaid API for live account balances and aggregates database telemetry.
        public async Task OnGetAsync()
        {
            var userId = GetUserId();
            // Fetch real, live account balances directly from Plaid API converted to ZAR
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

            // Compute dynamic database transaction telemetry
            try
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var startOfMonth = new DateOnly(today.Year, today.Month, 1);
                var thisMonthTxns = await _db.Transactions
                    .Where(t => t.UserID == userId && t.Date >= startOfMonth && t.Date <= today)
                    .ToListAsync();

                if (thisMonthTxns.Any())
                {
                    MonthlyOutflow = thisMonthTxns.Where(t => t.Amount > 0).Sum(t => t.Amount);
                    MonthlyInflow = thisMonthTxns.Where(t => t.Amount < 0).Sum(t => -t.Amount);
                    if (MonthlyInflow > 0)
                    {
                        var saved = MonthlyInflow - MonthlyOutflow;
                        SavingsRate = Math.Max(0, (saved / MonthlyInflow) * 100m);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compute dashboard metrics on Index OnGet");
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
