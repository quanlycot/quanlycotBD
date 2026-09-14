using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuanLyCotWeb.Controllers
{
    [Authorize]
    public class QuanLyController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}