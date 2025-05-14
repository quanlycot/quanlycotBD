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

        // Trang chủ: nếu đã đăng nhập thì chuyển về Quản lý Cốt
        public IActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Cots");
            }

            return RedirectToAction("TrangTimKiem");
        }

        // Trang tìm kiếm: ẩn nếu đã đăng nhập
        public IActionResult TrangTimKiem()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Cots");
            }

            return View();
        }

        // Xử lý kết quả tìm kiếm
        public async Task<IActionResult> KetQuaTimKiem(string ten)
        {
            if (string.IsNullOrWhiteSpace(ten))
                return RedirectToAction("TrangTimKiem");

            var ketQua = await _context.Cots
                .Include(c => c.IdViTriNavigation)
                .Include(c => c.IdnguoiThanNavigation)
                .Where(c =>
                    c.Idcot.ToString() == ten ||                         // Tìm đúng ID
                    (c.Ho + " " + c.Ten).Contains(ten) ||               // Họ tên đầy đủ
                    c.Ho.Contains(ten) || c.Ten.Contains(ten) ||        // Họ hoặc Tên riêng
                    c.PhapDanh.Contains(ten))                           // Pháp danh
                .ToListAsync();

            return View(ketQua);
        }

        // Mặc định
        public IActionResult Privacy()
        {
            return View();
        }

        // Lỗi
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
