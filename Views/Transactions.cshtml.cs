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
        public List<string> AvailableCategories { get; set; } = new();
        public decimal TotalSpent { get; set; }
        public decimal TotalIncome { get; set; }
        public int PlaidCount { get; set; }
        public int CsvCount { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SourceFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? CategoryFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "default-user";

            try
            {
                var query = _db.Transactions.Where(t => t.UserID == userId);

                AvailableCategories = await query
                    .Where(t => t.Category != null)
                    .Select(t => t.Category!)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToListAsync();

                if (!string.IsNullOrWhiteSpace(SourceFilter) && SourceFilter != "all")
                {
                    query = query.Where(t => t.Source.ToLower() == SourceFilter.ToLower());
                }

                if (!string.IsNullOrWhiteSpace(CategoryFilter) && CategoryFilter != "all")
                {
                    query = query.Where(t => t.Category != null && t.Category.ToLower() == CategoryFilter.ToLower());
                }

                if (!string.IsNullOrWhiteSpace(SearchTerm))
                {
                    var term = SearchTerm.ToLower();
                    query = query.Where(t => (t.Merchant != null && t.Merchant.ToLower().Contains(term)) ||
                                             (t.Category != null && t.Category.ToLower().Contains(term)));
                }

                Transactions = await query.OrderByDescending(t => t.Date).ToListAsync();

                TotalSpent = Transactions.Where(t => t.Amount > 0).Sum(t => t.Amount);
                TotalIncome = Transactions.Where(t => t.Amount < 0).Sum(t => -t.Amount);
                PlaidCount = Transactions.Count(t => t.Source.Equals("plaid", StringComparison.OrdinalIgnoreCase));
                CsvCount = Transactions.Count(t => t.Source.Equals("csv", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve transactions for user {UserId}", userId);
            }

            return Page();
        }
    }
}