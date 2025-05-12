using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyCotWeb.Models;

namespace QuanLyCotWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        private readonly QuanLyCotContext _context;

        public HomeController(QuanLyCotContext context)
        {
            _context = context;
       
        }


        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        public IActionResult TrangTimKiem()
        {
            return View();
        }

        public async Task<IActionResult> KetQuaTimKiem(string ten)
        {
            if (string.IsNullOrWhiteSpace(ten)) return RedirectToAction("TrangTimKiem");

            var ketQua = await _context.Cots
                .Include(c => c.IdViTriNavigation)
                .Include(c => c.IdnguoiThanNavigation)
                .Where(c => (c.Ho + " " + c.Ten).Contains(ten))
                .ToListAsync();

            return View(ketQua); // tạo View tương ứng phía dưới
        }

    }
}
