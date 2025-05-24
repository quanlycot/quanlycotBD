using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyCotWeb.Models;
using X.PagedList;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient; // KHÔNG dùng System.Data.SqlClient
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ClosedXML.Excel;
using QuanLyCotWeb.Services;


namespace QuanLyCotWeb.Controllers
{
   
    public class CotsController : Controller
    {
        private readonly QuanLyCotContext _context;


        // GET: Cots
        public async Task<IActionResult> Index(string searchString, int? page)
        {
            int pageSize = 20;
            int pageNumber = page ?? 1;

            var danhSach = _context.Cots
                .Include(c => c.IdViTriNavigation)
                .Include(c => c.IdnguoiThanNavigation)
                .OrderBy(c => c.Idcot)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                if (int.TryParse(searchString, out int id))
                {
                    // Nếu nhập là số → tìm đúng IDcot
                    danhSach = danhSach.Where(c => c.Idcot == id);
                }
                else
                {
                    // Nếu không phải số → tìm họ tên, pháp danh
                    danhSach = danhSach.Where(c =>
                        (c.Ho + " " + c.Ten).Contains(searchString) ||
                        c.Ho.Contains(searchString) ||
                        c.Ten.Contains(searchString) ||
                        c.PhapDanh.Contains(searchString));
                }
            }


            return View(await danhSach.ToPagedListAsync(pageNumber, pageSize));
        }

        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly BlobService _blobService;

        public CotsController(QuanLyCotContext context, IWebHostEnvironment hostEnvironment, BlobService blobService)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
            _blobService = blobService;
        }
        
        // GET: Cots/CreateFromViTri
        [Authorize]
        public async Task<IActionResult> CreateFromViTri(int idViTri)
        {
            var viTri = await _context.ViTris.FindAsync(idViTri);
            if (viTri == null) return NotFound();

            // Tìm cốt đang dùng vị trí này (nếu có)
            var cot = await _context.Cots.FirstOrDefaultAsync(c => c.IdviTri == idViTri);

            if (cot == null)
            {
                // Nếu chưa có thì tạo cốt mới
                cot = new Cot
                {
                  IdviTri = idViTri,  
                };
            }

            // Gán tình trạng hiện tại của vị trí cho ViewBag
            ViewBag.IdTinhTrang = viTri.IdTinhTrang;

            // Gửi danh sách tình trạng (dropdown) và chọn sẵn đúng giá trị hiện tại
            ViewBag.TinhTrangList = new SelectList(
                _context.TinhTrangs.ToList(),
                "IdTinhTrang",
                "TenTinhTrang",
                viTri.IdTinhTrang
            );

            return View("CreateFromViTri", cot);
        }

        // POST: Cots/CreateFromViTri
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFromViTri(Cot cot, int IdTinhTrang, IFormFile? HinhAnhUpload)
        {
            if (ModelState.IsValid)
            {
                // Cập nhật tình trạng vị trí
                var viTri = await _context.ViTris.FindAsync(cot.IdviTri);
                if (viTri != null)
                {
                    viTri.IdTinhTrang = IdTinhTrang;
                }

                var existingCot = await _context.Cots.FirstOrDefaultAsync(c => c.IdviTri == cot.IdviTri);

                if (existingCot != null)
                {
                    // Cập nhật cốt đã có
                    existingCot.Ho = cot.Ho;
                    existingCot.Ten = cot.Ten;
                    existingCot.PhapDanh = cot.PhapDanh;
                    existingCot.MatAl = cot.MatAl;
                    existingCot.MatDl = cot.MatDl;
                    existingCot.Tuoi = cot.Tuoi;
                    existingCot.NgayBatDau = cot.NgayBatDau;
                    existingCot.NgayKetThuc = cot.NgayKetThuc;
                    existingCot.NamSinh = cot.NamSinh;
                    existingCot.IdnguoiThan = cot.IdnguoiThan;

                    // Nếu có ảnh mới thì upload lên Azure
                    if (HinhAnhUpload != null && HinhAnhUpload.Length > 0)
                    {
                        var fileName = $"{existingCot.Idcot}.jpg";

                        using (var stream = HinhAnhUpload.OpenReadStream())
                        {
                            var blobUrl = await _blobService.UploadAsync(stream, fileName);
                            existingCot.HinhNguoiMat = blobUrl;
                        }
                    }

                    _context.Update(existingCot);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // Thêm mới cốt
                    _context.Cots.Add(cot);
                    await _context.SaveChangesAsync(); // Lúc này IDcot đã tự sinh

                    // Nếu có ảnh mới thì upload ảnh theo ID vừa sinh
                    if (HinhAnhUpload != null && HinhAnhUpload.Length > 0)
                    {
                        var fileName = $"{cot.Idcot}.jpg";

                        var blobUrl = await _blobService.UploadAsync(HinhAnhUpload.OpenReadStream(), fileName);
                        cot.HinhNguoiMat = blobUrl;

                        _context.Update(cot);
                        await _context.SaveChangesAsync();
                    }
                }

                TempData["SuccessMessage"] = "Lưu thông tin cốt thành công!";

                // Tính trang để quay lại vị trí vừa thao tác
                int index = await _context.ViTris
                    .Where(v => v.IdviTri < cot.IdviTri)
                    .CountAsync();

                int pageSize = 20;
                int page = (index / pageSize) + 1;

                return RedirectToAction("Index", "ViTris", new { page = page, highlight = cot.IdviTri });
            }

            // Trường hợp có lỗi nhập liệu, load lại dropdown
            ViewBag.TinhTrangList = new SelectList(_context.TinhTrangs.ToList(), "IdTinhTrang", "TenTinhTrang");
            return View("CreateFromViTri", cot);
        }


        // GET: Cots/XUẤT ECXEL
        [Authorize]
        // 1. Xuất Excel Cốt
        public IActionResult XuatCot()
        {
            var danhSach = _context.Cots
                .Include(c => c.IdViTriNavigation)
                .Include(c => c.IdnguoiThanNavigation)
                .OrderBy(c => c.Idcot)
                .AsNoTracking()
                .ToList();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Danh sách Cốt");

            var header = new[] {
            "ID Cốt", "Họ", "Tên", "Pháp danh", "Năm sinh", "Mất AL", "Mất DL", "Tuổi",
            "Bắt đầu", "Kết thúc", "Ảnh", "ID Vị Trí","ID Người Thân"
        };
            for (int i = 0; i < header.Length; i++)
                ws.Cell(1, i + 1).Value = header[i];

            int row = 2;
            foreach (var c in danhSach)
            {
                ws.Cell(row, 1).Value = c.Idcot;
                ws.Cell(row, 2).Value = c.Ho;
                ws.Cell(row, 3).Value = c.Ten;
                ws.Cell(row, 4).Value = c.PhapDanh;
                ws.Cell(row, 5).Value = c.NamSinh;
                ws.Cell(row, 6).Value = c.MatAl;
                ws.Cell(row, 7).Value = c.MatDl;
                ws.Cell(row, 8).Value = c.Tuoi;
                ws.Cell(row, 9).Value = c.NgayBatDau?.ToString("dd/MM/yyyy");
                ws.Cell(row, 10).Value = c.NgayKetThuc?.ToString("dd/MM/yyyy");
                ws.Cell(row, 11).Value = c.HinhNguoiMat;
                ws.Cell(row, 12).Value = c.IdviTri;
                ws.Cell(row, 13).Value = c.IdnguoiThan;
                row++;
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            string fileName = $"Cot_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // 2. Xuất Excel Người Thân
        public IActionResult XuatNguoiThan()
        {
            var danhSach = _context.NguoiThans
                .OrderBy(n => n.IdnguoiThan)
                .AsNoTracking()
                .ToList();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("NguoiThan");
            var header = new[] { "ID", "Họ", "Tên", "Pháp danh", "Ngày sinh", "CCCD", "Ngày cấp", "Nơi cấp", "Địa chỉ", "SĐT","ngày Đk","Chi Chú" };
            for (int i = 0; i < header.Length; i++)
                ws.Cell(1, i + 1).Value = header[i];

            int row = 2;
            foreach (var n in danhSach)
            {
                ws.Cell(row, 1).Value = n.IdnguoiThan;
                ws.Cell(row, 2).Value = n.Ho;
                ws.Cell(row, 3).Value = n.Ten;
                ws.Cell(row, 4).Value = n.PhapDanh;
                ws.Cell(row, 5).Value = n.NgaySinh;
                ws.Cell(row, 6).Value = n.Cccd;
                ws.Cell(row, 7).Value = n.NgayCap;
                ws.Cell(row, 8).Value = n.NoiCap;
                ws.Cell(row, 9).Value = n.DiaChi;
                ws.Cell(row, 10).Value = n.SoDienThoai;
                ws.Cell(row, 11).Value = n.NgayDangKy;
                ws.Cell(row, 12).Value = n.GhiChu;
                row++;
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            string fileName = $"NguoiThan_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // 3. Xuất Excel Vị trí
        public IActionResult XuatViTri()
        {
            var danhSach = _context.ViTris
                .OrderBy(v => v.IdviTri)
                .AsNoTracking()
                .ToList();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("ViTri");
            var header = new[] { "ID Vị Trí", "Lầu", "Lô số", "ID Tình Trạng" };
            for (int i = 0; i < header.Length; i++)
                ws.Cell(1, i + 1).Value = header[i];

            int row = 2;
            foreach (var v in danhSach)
            {
                ws.Cell(row, 1).Value = v.IdviTri;
                ws.Cell(row, 2).Value = v.Lau;
                ws.Cell(row, 3).Value = v.LoSo;
                ws.Cell(row, 4).Value = v.IdTinhTrang;
                row++;
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            string fileName = $"ViTri_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }


        // GET: Cots/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id, int? idNguoiThan)
        {
            if (id == null)
                return NotFound();

            var cot = await _context.Cots.FindAsync(id);
            if (cot == null)
                return NotFound();

            ViewBag.IdNguoiThan = idNguoiThan; // giữ lại để redirect sau
            return View(cot);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Idcot,Ho,Ten,PhapDanh,NamSinh,MatAl,MatDl,Tuoi,NgayBatDau,NgayKetThuc,HinhNguoiMat,IdviTri,IdnguoiThan")] Cot cot, IFormFile? HinhAnhUpload, int? idNguoiThan)
        {
            if (id != cot.Idcot)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Nếu có ảnh mới thì upload lên Azure
                    if (HinhAnhUpload != null && HinhAnhUpload.Length > 0)
                    {
                        var fileName = $"{cot.Idcot}.jpg";
                        using var stream = HinhAnhUpload.OpenReadStream();
                        var blobUrl = await _blobService.UploadAsync(stream, fileName);
                        cot.HinhNguoiMat = blobUrl;
                    }
                    else
                    {
                        // Giữ lại ảnh cũ nếu không có ảnh mới
                        var existing = await _context.Cots.AsNoTracking().FirstOrDefaultAsync(c => c.Idcot == id);
                        if (existing != null)
                            cot.HinhNguoiMat = existing.HinhNguoiMat;
                    }
                    // Lấy vị trí cũ của cốt (trước khi chuyển)
                    var cotCu = await _context.Cots.AsNoTracking().FirstOrDefaultAsync(c => c.Idcot == id);
                    if (cotCu != null && cotCu.IdviTri != cot.IdviTri)
                    {
                        // Cập nhật vị trí cũ thành Trống (3)
                        var viTriCu = await _context.ViTris.FindAsync(cotCu.IdviTri);
                        if (viTriCu != null)
                            viTriCu.IdTinhTrang = 3;

                        // Cập nhật vị trí mới thành Đã Có Cốt (1)
                        var viTriMoi = await _context.ViTris.FindAsync(cot.IdviTri);
                        if (viTriMoi != null)
                            viTriMoi.IdTinhTrang = 1;
                    }

                    _context.Update(cot);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Cập nhật thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CotExists(cot.Idcot)) return NotFound();
                    throw;
                }
                if (cot.IdnguoiThan.HasValue)
                {
                    return RedirectToAction("ThongKe", "NguoiThans", new { id = cot.IdnguoiThan.Value });
                }
                else
                {
                    var danhSach = _context.Cots.OrderBy(c => c.Idcot).AsEnumerable().ToList();
                    int index = danhSach.FindIndex(c => c.Idcot == cot.Idcot);
                    int page = index / 20 + 1;
                    return RedirectToAction("Index", new { page, highlight = cot.Idcot });
                }

            }

            return View(cot);
        }


        // GET: Cots/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var cot = await _context.Cots
                .Include(c => c.IdViTriNavigation)
                .Include(c => c.IdnguoiThanNavigation)
                .FirstOrDefaultAsync(m => m.Idcot == id);
            if (cot == null)
                return NotFound();

            return View(cot);
        }

        // GET: Cots/Delete/5
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var cot = await _context.Cots
                .Include(c => c.IdViTriNavigation)
                .Include(c => c.IdnguoiThanNavigation)
                .FirstOrDefaultAsync(m => m.Idcot == id);
            if (cot == null)
                return NotFound();

            return View(cot);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var cot = await _context.Cots.FindAsync(id);
            if (cot != null)
            {
                // Tìm vị trí chứa cốt
                var viTri = await _context.ViTris.FirstOrDefaultAsync(v => v.IdviTri == cot.IdviTri);

                // Nếu vị trí có tình trạng là "Đã Có Cốt" (1) hoặc "Đặc Chỗ" (2) thì cập nhật lại thành "Trống" (3)
                if (viTri != null && (viTri.IdTinhTrang == 1 || viTri.IdTinhTrang == 2))
                {
                    viTri.IdTinhTrang = 3;
                    _context.ViTris.Update(viTri);
                }

                // Xóa ảnh trên Azure nếu có đường link hợp lệ
                if (!string.IsNullOrEmpty(cot.HinhNguoiMat))
                {
                    var fileName = Path.GetFileName(new Uri(cot.HinhNguoiMat).LocalPath);
                    await _blobService.DeleteAsync(fileName);
                }

                _context.Cots.Remove(cot);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Đã xóa cốt: {cot.Ho} {cot.Ten} và cập nhật tình trạng vị trí.";
            }

            return RedirectToAction(nameof(Index));
        }




        private bool CotExists(int id)
        {
            return _context.Cots.Any(e => e.Idcot == id);
        }
    }
}
