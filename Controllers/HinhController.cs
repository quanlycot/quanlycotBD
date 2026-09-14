using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using QuanLyCotWeb.Models;
using QuanLyCotWeb.Services;
using X.PagedList;
using X.PagedList.Extensions;

namespace QuanLyCotWeb.Controllers
{
    [Authorize]
    public class HinhController : Controller
    {
        private readonly QuanLyCotContext _context;
        private readonly BlobService _blobService;

        public HinhController(QuanLyCotContext context, BlobService blobService)
        {
            _context = context;
            _blobService = blobService;
        }

        // ==============================================================
        // 1. DANH SÁCH HÌNH THỜ & TÌM KIẾM
        // ==============================================================
        [AllowAnonymous]
        public IActionResult Index(string searchString, int? namKetThuc, int? page)
        {
            int pageSize = 20;
            int pageNumber = page ?? 1;

            var danhSach = _context.HT_Hinh
                .Include(h => h.ViTri)
                .Include(h => h.NguoiThan)
                .OrderBy(h => h.IDHinh)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                if (int.TryParse(searchString, out int id))
                {
                    danhSach = danhSach.Where(h => h.IDHinh == id);
                }
                else
                {
                    danhSach = danhSach.Where(h =>
                        (h.Ho + " " + h.Ten).Contains(searchString) ||
                        h.Ho.Contains(searchString) ||
                        h.Ten.Contains(searchString) ||
                        h.PhapDanh.Contains(searchString));
                }
            }

            if (namKetThuc.HasValue)
            {
                danhSach = danhSach.Where(h => h.NgayKetThuc != null && h.NgayKetThuc.Value.Year <= namKetThuc.Value);
            }

            return View(danhSach.ToPagedList(pageNumber, pageSize));
        }

        // ==============================================================
        // 2. CHỨC NĂNG TIẾP NHẬN HÌNH THỜ 3 TẦNG (ALL-IN-ONE)
        // ==============================================================

        // 2.1. API Kiểm tra vị trí Tủ - Dãy (Tầng 2)
        [HttpGet]
        public async Task<IActionResult> KiemTraViTri(string? tu, string? day)
        {
            if (string.IsNullOrWhiteSpace(tu))
                return Json(new { success = false, message = "Vui lòng nhập tên Tủ." });

            tu = tu.Trim();
            day = day?.Trim() ?? "";

            var viTri = await _context.HT_ViTri
                .FirstOrDefaultAsync(v => v.Tu.ToLower() == tu.ToLower() && (v.Day ?? "").ToLower() == day.ToLower());

            if (viTri != null)
            {
                var daCoHinh = await _context.HT_Hinh.AnyAsync(h => h.IDViTri == viTri.IDViTri);
                if (daCoHinh)
                {
                    return Json(new { success = false, message = $"Vị trí Tủ {viTri.Tu} - Dãy {viTri.Day} đã có hình thờ an vị!" });
                }

                return Json(new
                {
                    success = true,
                    exists = true,
                    idViTri = viTri.IDViTri,
                    message = $"Vị trí hợp lệ: Tủ {viTri.Tu} - Dãy {viTri.Day} (Chưa có hình)"
                });
            }

            return Json(new
            {
                success = true,
                exists = false,
                message = "Vị trí chưa có trong hệ thống. Bạn có muốn thêm vị trí này?"
            });
        }

        // 2.2. API Tìm kiếm người thân danh bạ Hình Thờ (Tầng 3)
        [HttpGet]
        public async Task<IActionResult> TimNguoiThan(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return Json(new List<object>());

            keyword = keyword.Trim().ToLower();

            var ds = await _context.HT_NguoiThan
                .Where(n => (n.Ho + " " + n.Ten).ToLower().Contains(keyword)
                         || (n.SoDienThoai ?? "").Contains(keyword)
                         || (n.CCCD ?? "").Contains(keyword))
                .Take(10)
                .Select(n => new
                {
                    id = n.IDNguoiThan,
                    ho = n.Ho,
                    ten = n.Ten,
                    phapDanh = n.PhapDanh,
                    sdt = n.SoDienThoai,
                    diaChi = n.DiaChi,
                    cccd = n.CCCD,
                    ngaySinh = n.NamSinh
                })
                .ToListAsync();

            return Json(ds);
        }

        // 2.3. POST: Lưu toàn bộ hồ sơ 3 tầng & Tính toán số trang chính xác
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> CreateAllInOne(
            Hinh hinh,
            IFormFile? HinhAnhUpload,
            bool TaoViTriMoi, string? VT_Tu, string? VT_Day,
            bool TaoNguoiThanMoi,
            string? NT_Ho, string? NT_Ten, string? NT_PhapDanh,
            string? NT_NamSinh, string? NT_CCCD, string? NT_DiaChi, string? NT_SDT)
        {
            try
            {
                // --- XỬ LÝ TẦNG 2: VỊ TRÍ (TỦ - DÃY) ---
                if (TaoViTriMoi && !string.IsNullOrWhiteSpace(VT_Tu))
                {
                    var vtMoi = new HT_ViTri
                    {
                        Tu = VT_Tu.Trim(),
                        Day = VT_Day?.Trim() ?? ""
                    };
                    _context.HT_ViTri.Add(vtMoi);
                    await _context.SaveChangesAsync();
                    hinh.IDViTri = vtMoi.IDViTri;
                }

                // --- XỬ LÝ TẦNG 3: NGƯỜI THÂN ---
                if (TaoNguoiThanMoi && !string.IsNullOrWhiteSpace(NT_Ten))
                {
                    var ntMoi = new HT_NguoiThan
                    {
                        Ho = NT_Ho?.Trim() ?? "",
                        Ten = NT_Ten.Trim(),
                        PhapDanh = NT_PhapDanh?.Trim(),
                        NamSinh = NT_NamSinh?.Trim(),
                        CCCD = NT_CCCD?.Trim(),
                        DiaChi = NT_DiaChi?.Trim(),
                        SoDienThoai = NT_SDT?.Trim()
                    };
                    _context.HT_NguoiThan.Add(ntMoi);
                    await _context.SaveChangesAsync();
                    hinh.IDNguoiThan = ntMoi.IDNguoiThan;
                }
                else if (hinh.IDNguoiThan.HasValue && hinh.IDNguoiThan.Value > 0)
                {
                    var ntCu = await _context.HT_NguoiThan.FindAsync(hinh.IDNguoiThan.Value);
                    if (ntCu != null)
                    {
                        if (!string.IsNullOrWhiteSpace(NT_SDT)) ntCu.SoDienThoai = NT_SDT.Trim();
                        if (!string.IsNullOrWhiteSpace(NT_DiaChi)) ntCu.DiaChi = NT_DiaChi.Trim();
                        if (!string.IsNullOrWhiteSpace(NT_CCCD)) ntCu.CCCD = NT_CCCD.Trim();
                        _context.HT_NguoiThan.Update(ntCu);
                        await _context.SaveChangesAsync();
                    }
                }

                // --- XỬ LÝ TẦNG 1: THÔNG TIN HÌNH THỜ (ĐÃ BAO GỒM LinkAnh) & ẢNH ---
                _context.HT_Hinh.Add(hinh);
                await _context.SaveChangesAsync(); // Sinh IDHinh

                if (HinhAnhUpload != null && HinhAnhUpload.Length > 0)
                {
                    var fileName = $"HT{hinh.IDHinh}.jpg";
                    using var stream = HinhAnhUpload.OpenReadStream();
                    var blobUrl = await _blobService.UploadAsync(stream, fileName);
                    hinh.AnhHinh = blobUrl;
                    _context.HT_Hinh.Update(hinh);
                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = $"Tiếp nhận thành công hồ sơ hình thờ: {hinh.Ho} {hinh.Ten} (Mã #{hinh.IDHinh})!";

                // --- TÍNH TOÁN SỐ TRANG CHỨA DÒNG VỪA THÊM THEO CHUẨN CỐT ---
                int pageSize = 20;
                int viTriDung = await _context.HT_Hinh.CountAsync(h => h.IDHinh <= hinh.IDHinh);
                int pageDich = (int)Math.Ceiling((double)viTriDung / pageSize);
                if (pageDich <= 0) pageDich = 1;

                return RedirectToAction(nameof(Index), new { page = pageDich, highlight = hinh.IDHinh });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi lưu dữ liệu hình thờ: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // ==============================================================
        // 3. THÊM TỪ VỊ TRÍ
        // ==============================================================
        [Authorize]
        public async Task<IActionResult> CreateFromViTri(int idViTri)
        {
            var viTri = await _context.HT_ViTri.FindAsync(idViTri);
            if (viTri == null) return NotFound();

            var hinh = await _context.HT_Hinh.FirstOrDefaultAsync(h => h.IDViTri == idViTri);
            if (hinh == null)
            {
                hinh = new Hinh
                {
                    IDViTri = idViTri,
                    NgayBatDau = DateTime.Today,
                    NgayKetThuc = DateTime.Today.AddYears(10)
                };
            }

            return View("CreateFromViTri", hinh);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> CreateFromViTri(Hinh hinh, IFormFile? HinhAnhUpload)
        {
            if (!ModelState.IsValid)
                return View("CreateFromViTri", hinh);

            var existing = await _context.HT_Hinh.FirstOrDefaultAsync(h => h.IDViTri == hinh.IDViTri);

            if (existing != null)
            {
                existing.Ho = hinh.Ho;
                existing.Ten = hinh.Ten;
                existing.PhapDanh = hinh.PhapDanh;
                existing.NamSinh = hinh.NamSinh;
                existing.Tuoi = hinh.Tuoi;
                existing.NgayBatDau = hinh.NgayBatDau;
                existing.NgayKetThuc = hinh.NgayKetThuc;
                existing.NgayMatAL = hinh.NgayMatAL;
                existing.NgayMatDL = hinh.NgayMatDL;
                existing.IDNguoiThan = hinh.IDNguoiThan;
                existing.LinkAnh = hinh.LinkAnh; // ✅ Cập nhật LinkAnh khi sửa hồ sơ sẵn có

                if (HinhAnhUpload != null && HinhAnhUpload.Length > 0)
                {
                    var fileName = $"HT{existing.IDHinh}.jpg";
                    var blobUrl = await _blobService.UploadAsync(HinhAnhUpload.OpenReadStream(), fileName);
                    existing.AnhHinh = blobUrl;
                }
                else
                {
                    var old = await _context.HT_Hinh.AsNoTracking().FirstOrDefaultAsync(h => h.IDHinh == existing.IDHinh);
                    if (old != null)
                        existing.AnhHinh = old.AnhHinh;
                }

                _context.Update(existing);
                await _context.SaveChangesAsync();
            }
            else
            {
                _context.HT_Hinh.Add(hinh);
                await _context.SaveChangesAsync();

                if (HinhAnhUpload != null && HinhAnhUpload.Length > 0)
                {
                    var fileName = $"HT{hinh.IDHinh}.jpg";
                    var blobUrl = await _blobService.UploadAsync(HinhAnhUpload.OpenReadStream(), fileName);
                    hinh.AnhHinh = blobUrl;

                    _context.Update(hinh);
                    await _context.SaveChangesAsync();
                }
            }

            TempData["SuccessMessage"] = "Lưu thông tin hình thờ thành công!";

            int pageSize = 20;
            var danhSach = await _context.HT_ViTri.OrderBy(v => v.Tu).ThenBy(v => v.Day).ToListAsync();
            int index = danhSach.FindIndex(v => v.IDViTri == hinh.IDViTri);
            int page = (index / pageSize) + 1;

            return RedirectToAction("Index", "HT_ViTri", new { page = page, highlight = hinh.IDViTri });
        }

        // ==============================================================
        // 4. CHI TIẾT HÌNH THỜ
        // ==============================================================
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var hinh = await _context.HT_Hinh
                .Include(h => h.ViTri)
                .Include(h => h.NguoiThan)
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.IDHinh == id);

            if (hinh == null) return NotFound();

            return View(hinh);
        }

        // ==============================================================
        // 5. CHỈNH SỬA HÌNH THỜ
        // ==============================================================
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var hinh = await _context.HT_Hinh.FindAsync(id);
            if (hinh == null) return NotFound();

            return View(hinh);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(int id, Hinh hinh, IFormFile? HinhAnhUpload)
        {
            if (id != hinh.IDHinh) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    if (HinhAnhUpload != null && HinhAnhUpload.Length > 0)
                    {
                        var fileName = $"HT{hinh.IDHinh}.jpg";
                        var blobUrl = await _blobService.UploadAsync(HinhAnhUpload.OpenReadStream(), fileName);
                        hinh.AnhHinh = blobUrl;
                    }
                    else
                    {
                        var existing = await _context.HT_Hinh.AsNoTracking().FirstOrDefaultAsync(h => h.IDHinh == id);
                        if (existing != null)
                            hinh.AnhHinh = existing.AnhHinh;
                    }

                    // Entity State được Update toàn bộ bao gồm cả trường LinkAnh mới sửa
                    _context.Update(hinh);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Cập nhật hình thờ thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.HT_Hinh.Any(e => e.IDHinh == id)) return NotFound();
                    throw;
                }

                int pageSize = 20;
                int viTriDung = await _context.HT_Hinh.CountAsync(h => h.IDHinh <= hinh.IDHinh);
                int pageDich = (int)Math.Ceiling((double)viTriDung / pageSize);
                if (pageDich <= 0) pageDich = 1;

                return RedirectToAction(nameof(Index), new { page = pageDich, highlight = hinh.IDHinh });
            }

            return View(hinh);
        }

        // ==============================================================
        // 6. XÓA HÌNH THỜ
        // ==============================================================
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var hinh = await _context.HT_Hinh
                .Include(h => h.ViTri)
                .Include(h => h.NguoiThan)
                .FirstOrDefaultAsync(h => h.IDHinh == id);

            if (hinh == null) return NotFound();

            return View(hinh);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var hinh = await _context.HT_Hinh.FindAsync(id);
            if (hinh != null)
            {
                if (!string.IsNullOrEmpty(hinh.AnhHinh))
                {
                    try
                    {
                        var fileName = Path.GetFileName(new Uri(hinh.AnhHinh).LocalPath);
                        await _blobService.DeleteAsync(fileName);
                    }
                    catch
                    {
                    }
                }

                _context.HT_Hinh.Remove(hinh);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Đã xóa hình thờ thành công.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}