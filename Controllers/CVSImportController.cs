using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoneyBot.Services;
using System.Security.Claims;

namespace MoneyBot.Controllers
{
    [ApiController]
    [Route("api/csv-import")]
    [Authorize]
    public class CVSImportController : Controller
    {


        private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!; 

        private readonly ImportCSV _csvImportService;
        public CVSImportController(ImportCSV ImportCSV)
        {
            _csvImportService = ImportCSV;
        }


        [HttpPost]
        public async Task<IActionResult> Import([FromForm] string bankName, [FromForm] IFormFile file)
        {
            if (file.Length == 0)
                return BadRequest("No file uploaded.");

            var userId = GetUserId();
            using var stream = file.OpenReadStream();

            try
            {
                var count = await _csvImportService.ImportAsync(userId, bankName, stream);
                return Ok(new { imported = count });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        
    }
}
