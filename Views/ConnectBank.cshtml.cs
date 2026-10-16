using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MoneyBot.Services;

namespace MoneyBot.Views
{
    [IgnoreAntiforgeryToken(Order = 1001)]
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
                ErrorMessage = ex.Message ?? "Could not initialize bank connection. Please check Plaid configuration.";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostExchangeAsync([FromForm] string publicToken, [FromForm] string? institutionName)
        {
            if (string.IsNullOrWhiteSpace(publicToken))
            {
                ErrorMessage = "Invalid public token received from bank connection.";
                return Page();
            }

            try
            {
                var userId = GetUserId();
                await _plaidService.ExchangePublicTokenAsync(userId, publicToken, institutionName);
                return RedirectToPage("/Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exchanging public token for user.");
                var rootEx = ex.GetBaseException();
                ErrorMessage = $"Failed to connect bank: {rootEx.Message}";
                return Page();
            }
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "default-user";
        }
    }
}
