using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoneyBot.Services;
using System.Security.Claims;

namespace MoneyBot.Controllers
{
    [ApiController]
    [Route("api/mono")]
    [Authorize]
    public class MonoController
    {
  

        public class MonoControllers : Controller
        {
            private readonly MonoService _monoService;

            public MonoControllers(MonoService monoService)
            {
                _monoService = monoService;
            }

            [HttpPost("exchange")]
            public async Task<IActionResult> Exchange([FromBody] MonoExchangeRequest request)
            {
                var userId = GetUserId();
                await _monoService.ExchangeCodeAsync(userId, request.Code);
                return Ok();
            }

            [HttpPost("sync")]
            public async Task<IActionResult> Sync()
            {
                var userId = GetUserId();
                await _monoService.SyncTransactionsAsync(userId);
                return Ok();
            }

            private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        }

        public record MonoExchangeRequest(string Code);
    }

    }

