using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MoneyBot.Services;

namespace MoneyBot.Views
{
    public class ImportCsvModel : PageModel
    {
        private readonly ImportCSV _csvService;
        private readonly ILogger<ImportCsvModel> _logger;

        public ImportCsvModel(ImportCSV csvService, ILogger<ImportCsvModel> logger)
        {
            _csvService = csvService;
            _logger = logger;
        }

        [BindProperty]
        public string BankName { get; set; } = "FNB";

        [BindProperty]
        public IFormFile? CsvFile { get; set; }

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (CsvFile == null || CsvFile.Length == 0)
            {
                ErrorMessage = "Please select a valid CSV statement file.";
                return Page();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "default-user";

            try
            {
                using var stream = CsvFile.OpenReadStream();
                int count = await _csvService.ImportAsync(userId, BankName, stream);
                SuccessMessage = $"Successfully imported {count} transaction(s) from {BankName}.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CSV import failed for {Bank}", BankName);
                ErrorMessage = ex.Message;
            }

            return Page();
        }
    }
}