using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoneyBot.Services;
using System.Security.Claims;

namespace MoneyBot.Controllers
{

    [ApiController]
    [Route("api/plaid")]
    [Authorize]

    public class PlaidController : ControllerBase
    {

        private readonly PlaidService plaidService; 
        private readonly ILogger<PlaidController> logger;
        public record ExchangeRequest(string PublicToken);
        public PlaidController(PlaidService plaidService, ILogger<PlaidController> logger)
        {
            this.plaidService = plaidService;
            this.logger = logger;
        }

        [HttpPost("link-token")]
        public async Task<IActionResult> CreateLinkToken()
        {
            try
            {
                var linkToken = await plaidService.CreateLinkTokenAsync(GetUserId());
                return Ok(new { linkToken });
            }
            catch (InvalidOperationException ex)
            {
                return Problem(detail: ex.Message, statusCode: 502);
            }
        }

        [HttpPost("exchange")]
        public async Task<IActionResult> ExchangePublicToken([FromBody] ExchangeRequest request)
        {
            try
            {
                await plaidService.ExchangePublicTokenAsync(GetUserId(), request.PublicToken);
                return Ok();
            }
            catch (InvalidOperationException ex)
            {
                return Problem(detail: ex.Message, statusCode: 502);
            }
        }

        [HttpPost("sync")]
        public async Task<IActionResult> Sync()
        {
            await plaidService.SyncTransactionsAsync(GetUserId());
            return Ok();
        }

        private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    }





}

