using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MoneyBot.Data;
using MoneyBot.Models;

namespace MoneyBot.Views
{
    public class TransactionsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ILogger<TransactionsModel> _logger;

        public TransactionsModel(AppDbContext db, ILogger<TransactionsModel> logger)
        {
            _db = db;
            _logger = logger;
        }

        public List<Transaction> Transactions { get; set; } = new();
        public decimal TotalSpent { get; set; }
        public decimal TotalIncome { get; set; }
        public int PlaidCount { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "default-user";

            try
            {
                Transactions = await _db.Transactions
                    .Where(t => t.UserID == userId)
                    .OrderByDescending(t => t.Date)
                    .ToListAsync();

                TotalSpent = Transactions.Where(t => t.Amount > 0).Sum(t => t.Amount);
                TotalIncome = Transactions.Where(t => t.Amount < 0).Sum(t => -t.Amount);
                PlaidCount = Transactions.Count(t => t.Source.Equals("plaid", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve transactions for user {UserId}", userId);
            }

            return Page();
        }
    }
}
