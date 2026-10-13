using Microsoft.AspNetCore.Mvc;

namespace MoneyBot.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index() => View();
    }
}
