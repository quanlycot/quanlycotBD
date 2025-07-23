using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyCotWeb.Models;
using QuanLyCotWeb.Helpers;
using Azure.Core;
using X.PagedList.Extensions;
using X.PagedList;

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

        string keyword = StringHelper.NormalizeString(ten);
        var ketQua = new List<TimKiemViewModel>();

        // 🔹 Lấy toàn bộ dữ liệu Cốt rồi lọc bằng LINQ in-memory
        var dsCotRaw = await _context.Cots
            .Include(c => c.IdViTriNavigation)
            .Include(c => c.IdnguoiThanNavigation)
            .ToListAsync();

        var dsCot = dsCotRaw
            .Where(c =>
                c.Idcot.ToString() == ten ||
                StringHelper.NormalizeString(c.Ho + " " + c.Ten).Contains(keyword) ||
                StringHelper.NormalizeString(c.Ho).Contains(keyword) ||
                StringHelper.NormalizeString(c.Ten).Contains(keyword) ||
                StringHelper.NormalizeString(c.PhapDanh ?? "").Contains(keyword)
            )
            .Select(c => new TimKiemViewModel
            {
                Loai = "Cốt",
                ID = c.Idcot,
                Ho = c.Ho,
                Ten = c.Ten,
                PhapDanh = c.PhapDanh,
                NamSinh = c.NamSinh,
                NgayMatDL = c.MatDl,
                Tuoi = c.Tuoi,
                ViTriHienThi = "Lầu " + c.IdViTriNavigation?.Lau + " - Dãy " + c.IdViTriNavigation?.LoSo,
                TenNguoiThan = c.IdnguoiThanNavigation?.Ho + " " + c.IdnguoiThanNavigation?.Ten,
                AnhUrl = c.HinhNguoiMat
            }).ToList();

        ketQua.AddRange(dsCot);

        // 🔹 Lấy toàn bộ dữ liệu Hình Thờ rồi lọc bằng LINQ in-memory
        var dsHinhRaw = await _context.HT_Hinh
            .Include(h => h.ViTri)
            .Include(h => h.NguoiThan)
            .ToListAsync();

        var dsHinh = dsHinhRaw
            .Where(h =>
                h.IDHinh.ToString() == ten ||
                StringHelper.NormalizeString(h.Ho + " " + h.Ten).Contains(keyword) ||
                StringHelper.NormalizeString(h.Ho).Contains(keyword) ||
                StringHelper.NormalizeString(h.Ten).Contains(keyword) ||
                StringHelper.NormalizeString(h.PhapDanh ?? "").Contains(keyword)
            )
            .Select(h => new TimKiemViewModel
            {
                Loai = "Hình",
                ID = h.IDHinh,
                Ho = h.Ho,
                Ten = h.Ten,
                PhapDanh = h.PhapDanh,
                NamSinh = h.NamSinh,
                NgayMatDL = h.NgayMatDL,
                Tuoi = h.Tuoi,
                ViTriHienThi = "Tủ " + h.ViTri?.Tu + " - Dãy " + h.ViTri?.Day,
                TenNguoiThan = h.NguoiThan?.Ho + " " + h.NguoiThan?.Ten,
                AnhUrl = h.AnhHinh
            }).ToList();

        ketQua.AddRange(dsHinh);

        // 🔸 Phân trang kết quả
        int pageSize = 20;
        int pageNumber = 1;

        if (Request.Query.ContainsKey("page"))
        {
            int.TryParse(Request.Query["page"], out pageNumber);
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
        }

        return View(ketQua.ToPagedList(pageNumber, pageSize));
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
