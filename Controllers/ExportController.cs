// ExportController.cs
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyCotWeb.Models;
using System.IO;

namespace QuanLyCotWeb.Controllers
{
    [Authorize]
    public class ExportController : Controller
    {
        private readonly QuanLyCotContext _context;
        private readonly IWebHostEnvironment _env;

        public ExportController(QuanLyCotContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // Xuất bảng Cots
        [HttpPost]
        public async Task<IActionResult> XuatCot()
        {
            var danhSach = await _context.Cots
                .Include(c => c.IdViTriNavigation)
                .Include(c => c.IdnguoiThanNavigation)
                .OrderBy(c => c.Idcot)
                .ToListAsync();

            var fileName = $"Cot_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            var filePath = Path.Combine(_env.WebRootPath, "exports", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Cots");
            ws.Cell(1, 1).InsertTable(danhSach.Select(c => new
            {
                c.Idcot,
                c.Ho,
                c.Ten,
                c.PhapDanh,
                c.NamSinh,
                c.MatAl,
                c.MatDl,
                c.Tuoi,
                c.NgayBatDau,
                c.NgayKetThuc,
                c.HinhNguoiMat,
                c.IdviTri,
                c.IdnguoiThan,
            }), true);

            wb.SaveAs(filePath);
            TempData["SuccessMessage"] = $"Xuất file thành công: /exports/{fileName}";
            return RedirectToAction("Index", "Cots");
        }

        // Xuất bảng Người Thân
        [HttpPost]
        public async Task<IActionResult> XuatNguoiThan()
        {
            var ds = await _context.NguoiThans.OrderBy(n => n.IdnguoiThan).ToListAsync();
            var fileName = $"NguoiThan_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            var filePath = Path.Combine(_env.WebRootPath, "exports", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("NguoiThans");
            ws.Cell(1, 1).InsertTable(ds.Select(n => new
            {
                n.IdnguoiThan,
                n.Ho,
                n.Ten,
                n.PhapDanh,
                n.NgaySinh,
                n.Cccd,
                n.NgayCap,
                n.NoiCap,
                n.DiaChi,
                n.SoDienThoai,
                n.NgayDangKy,
                n.GhiChu
            }), true);

            wb.SaveAs(filePath);
            TempData["SuccessMessage"] = $"Xuất file thành công: /exports/{fileName}";
            return RedirectToAction("Index", "NguoiThans");
        }

        // Xuất bảng Vị Trí
        [HttpPost]
        public async Task<IActionResult> XuatViTri()
        {
            var ds = await _context.ViTris.OrderBy(v => v.IdviTri).ToListAsync();
            var fileName = $"ViTri_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            var filePath = Path.Combine(_env.WebRootPath, "exports", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("ViTris");
            ws.Cell(1, 1).InsertTable(ds.Select(v => new
            {
                v.IdviTri,
                v.Lau,
                v.LoSo,
                v.IdTinhTrang
            }), true);

            wb.SaveAs(filePath);
            TempData["SuccessMessage"] = $"Xuất file thành công: /exports/{fileName}";
            return RedirectToAction("Index", "ViTris");
        }
    }
}
