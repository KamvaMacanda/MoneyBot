using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoneyBot.Services;
using System.Security.Claims;

namespace MoneyBot.Controllers
{


    [ApiController]
    [Route("api/chat")]
    [Authorize]

    public class ChatController : Controller
    {

        private readonly ChatBotLogic Chatlogic;

        public record ChatRequest(string Message);

        public ChatController(ChatBotLogic logic)
        {
            Chatlogic = logic;
        }

        [HttpPost]
        public async Task<IActionResult> Ask([FromBody] ChatRequest request)
        {
            var userId = GetUserId();
            var reply = await Chatlogic.AskAsync(userId, request.Message);
            return Ok(new { reply });
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        }





    }

}
    