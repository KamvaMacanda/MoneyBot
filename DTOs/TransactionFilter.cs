using Microsoft.AspNetCore.Mvc;
using MoneyBot.Models;

namespace MoneyBot.DTOs
{

    /// <summary>
    /// DTO for encapsulating transaction filtering criteria and ledger metrics.
    /// </summary>
    public class TransactionFilter 
    {


        // Filter criteria bound from query string
        [BindProperty(SupportsGet = true)]
        public string? SourceFilter { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? CategoryFilter { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }


        // Presentation metrics calculated via LINQ
        public List<Transaction> Transactions { get; set; } = new();
        public List<string> AvailableCategories { get; set; } = new();
        public decimal TotalSpent { get; set; }
        public decimal TotalIncome { get; set; }
        public int PlaidCount { get; set; }
        public int CsvCount { get; set; }











    }
}
