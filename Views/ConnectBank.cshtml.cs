using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MoneyBot.Services;

namespace MoneyBot.Views
{
    public class ConnectBankModel : PageModel
    {
        private readonly PlaidService _plaidService;
        private readonly ILogger<ConnectBankModel> _logger;

        public ConnectBankModel(PlaidService plaidService, ILogger<ConnectBankModel> logger)
        {
            _plaidService = plaidService;
            _logger = logger;
        }

        public string? LinkToken { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var userId = GetUserId();
                LinkToken = await _plaidService.CreateLinkTokenAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Plaid link token on ConnectBank page.");
                ErrorMessage = "Could not initialize bank connection. Please verify your Plaid credentials and try again.";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostExchangeAsync([FromForm] string publicToken)
        {
            if (string.IsNullOrWhiteSpace(publicToken))
            {
                ErrorMessage = "Invalid public token received from bank connection.";
                return Page();
            }

            try
            {
                var userId = GetUserId();
                await _plaidService.ExchangePublicTokenAsync(userId, publicToken);
                return RedirectToPage("/Transactions");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exchanging public token for user.");
                ErrorMessage = "Failed to complete bank connection. Please try linking again.";
                return Page();
            }
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "default-user";
        }
    }
}
