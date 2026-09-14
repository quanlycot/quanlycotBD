using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyCotWeb.Models;
using X.PagedList.Extensions;
using X.PagedList;

namespace QuanLyCotWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly QuanLyCotContext _context;

        public HomeController(QuanLyCotContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return RedirectToAction("TrangTimKiem");
        }

        // Trang tra cứu: Đẩy điều kiện lọc xuống thẳng SQL Server và phân trang 20 mục
        public async Task<IActionResult> TrangTimKiem(string ten, string loai = "ALL", int page = 1)
        {
            string keyword = string.IsNullOrWhiteSpace(ten) ? "" : ten.Trim().ToLower();
            var ketQua = new List<TimKiemViewModel>();

            // 1. TÌM TRONG BẢNG CỐT TRỰC TIẾP TẠI SQL SERVER
            if (loai == "ALL" || loai == "COT")
            {
                var queryCot = _context.Cots.AsNoTracking();

                if (!string.IsNullOrEmpty(keyword))
                {
                    queryCot = queryCot.Where(c =>
                        c.Idcot.ToString() == keyword ||
                        (c.Ho + " " + c.Ten).ToLower().Contains(keyword) ||
                        (c.Ho != null && c.Ho.ToLower().Contains(keyword)) ||
                        (c.Ten != null && c.Ten.ToLower().Contains(keyword)) ||
                        (c.PhapDanh != null && c.PhapDanh.ToLower().Contains(keyword)) ||
                        (c.NamSinh != null && c.NamSinh.Contains(keyword)) ||
                        (c.MatDl != null && c.MatDl.Contains(keyword))
                    );
                }

                var dsCot = await queryCot
                    .OrderByDescending(c => c.Idcot)
                    .Take(string.IsNullOrEmpty(keyword) ? 6 : 100) // Lọc tối đa trên SQL
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
                        ViTriHienThi = c.IdViTriNavigation != null ? "Lầu " + c.IdViTriNavigation.Lau + " - Dãy " + c.IdViTriNavigation.LoSo : "Chưa cập nhật",
                        TenNguoiThan = c.IdnguoiThanNavigation != null ? c.IdnguoiThanNavigation.Ho + " " + c.IdnguoiThanNavigation.Ten : "",
                        AnhUrl = c.HinhNguoiMat
                    })
                    .ToListAsync();

                ketQua.AddRange(dsCot);
            }

            // 2. TÌM TRONG BẢNG HÌNH THỜ TRỰC TIẾP TẠI SQL SERVER
            if (loai == "ALL" || loai == "HINH_THO")
            {
                var queryHinh = _context.HT_Hinh.AsNoTracking();

                if (!string.IsNullOrEmpty(keyword))
                {
                    queryHinh = queryHinh.Where(h =>
                        h.IDHinh.ToString() == keyword ||
                        (h.Ho + " " + h.Ten).ToLower().Contains(keyword) ||
                        (h.Ho != null && h.Ho.ToLower().Contains(keyword)) ||
                        (h.Ten != null && h.Ten.ToLower().Contains(keyword)) ||
                        (h.PhapDanh != null && h.PhapDanh.ToLower().Contains(keyword)) ||
                        (h.NamSinh != null && h.NamSinh.Contains(keyword)) ||
                        (h.NgayMatDL != null && h.NgayMatDL.Contains(keyword))
                    );
                }

                var dsHinh = await queryHinh
                    .OrderByDescending(h => h.IDHinh)
                    .Take(string.IsNullOrEmpty(keyword) ? 6 : 100)
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
                        ViTriHienThi = h.ViTri != null ? "Tủ " + h.ViTri.Tu + " - Dãy " + h.ViTri.Day : "Chưa cập nhật",
                        TenNguoiThan = h.NguoiThan != null ? h.NguoiThan.Ho + " " + h.NguoiThan.Ten : "",
                        AnhUrl = h.AnhHinh
                    })
                    .ToListAsync();

                ketQua.AddRange(dsHinh);
            }

            ViewBag.TuKhoa = ten ?? "";
            ViewBag.LoaiHienTai = loai;

            // Phân trang 20 mục/trang
            int pageSize = 20;
            int pageNumber = page <= 0 ? 1 : page;

            return View(ketQua.ToPagedList(pageNumber, pageSize));
        }
    }
}