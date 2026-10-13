using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyBot.Data;
using System.Security.Claims;

namespace MoneyBot.Controllers
{

    [ApiController]
    [Route("api/transactions")]
    [Authorize]
        public class TransactionsController : Controller
    {

        private readonly AppDbContext _db;

        public TransactionsController(AppDbContext db)
        {
            _db = db;
        }

        //gets the ID of the logged-in user 
        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        }


        //Returing user transactions 
        [HttpGet]
        public async Task <ActionResult> GetAll()
        {
            var userId = GetUserId();
            var transactions = await _db.Transactions
                .Where(t => t.UserID == userId)
                .OrderByDescending(t => t.Date)
                .ToListAsync();

            return Ok(transactions); 

        }


        //changes the category of one transaction

        [HttpPatch("{id}/category")]
        public async Task<IActionResult> UpdateCategory(string id, [FromBody] string category)
        {
            var userId = GetUserId();
            var txn = await _db.Transactions.FirstOrDefaultAsync(
                t => t.TransactionId == id &&
                t.UserID == userId);

            if (txn == null)
            {
                return NotFound();
            }
          

            txn.Category = category;
            txn.IsCategoryManual = true;
            await _db.SaveChangesAsync();

            return Ok(txn);
        }




    }
}
