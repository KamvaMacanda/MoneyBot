using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyBot.Data;

namespace MoneyBot.Controllers
{

    [ApiController]
    [Route("api/transactions")]
    [Authorize]
        public class TransactionsController : ControllerBase
    {

        private readonly AppDbContext _db;

        public TransactionsController(AppDbContext db)
        {
            _db = db;
        }

        //Returing user transactions 
        [HttpGet]
        public async Task <ActionResult> GetAll()
        {
            var userId = GetUserId();
            var transactions = await _db.Transactions
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.Date)
                .ToListAsync();

            return Ok(transactions); 

        }






    }
}
