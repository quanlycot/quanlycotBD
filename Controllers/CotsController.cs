using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using ClosedXML.Excel;
using X.PagedList;
using X.PagedList.Extensions;
using QuanLyCotWeb.Models;
using QuanLyCotWeb.Services;

namespace QuanLyCotWeb.Controllers
{
    public class CotsController : Controller
    {
        private readonly QuanLyCotContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly BlobService _blobService;

        public CotsController(QuanLyCotContext context, IWebHostEnvironment hostEnvironment, BlobService blobService)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
            _blobService = blobService;
        }

        // ==========================================
        // 1. DANH SÁCH CỐT & TÌM KIẾM
        // ==========================================
        public IActionResult Index(string searchString, int? namKetThuc, int? page)
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
                searchString = searchString.Trim();
                if (int.TryParse(searchString, out int id))
                {
                    danhSach = danhSach.Where(c => c.Idcot == id);
                }
                else
                {
                    danhSach = danhSach.Where(c =>
                        (c.Ho + " " + c.Ten).Contains(searchString) ||
                        (c.Ho != null && c.Ho.Contains(searchString)) ||
                        (c.Ten != null && c.Ten.Contains(searchString)) ||
                        (c.PhapDanh != null && c.PhapDanh.Contains(searchString)));
                }
            }

            if (namKetThuc.HasValue)
            {
                danhSach = danhSach.Where(c => c.NgayKetThuc != null && c.NgayKetThuc.Value.Year <= namKetThuc.Value);
            }

            return View(danhSach.ToPagedList(pageNumber, pageSize));
        }

        // ==========================================
        // 2. CHỨC NĂNG TIẾP NHẬN 3 TẦNG (ALL-IN-ONE)
        // ==========================================

        // 2.1. API Kiểm tra vị trí (Tầng 2)
        [HttpGet]
        public async Task<IActionResult> KiemTraViTri(string lau, string loSo)
        {
            if (string.IsNullOrWhiteSpace(lau) || string.IsNullOrWhiteSpace(loSo))
                return Json(new { tonTai = false, message = "Vui lòng nhập đầy đủ Lầu và Lô số." });

            lau = lau.Trim();
            loSo = loSo.Trim();

            var vt = await _context.ViTris.FirstOrDefaultAsync(v => v.Lau == lau && v.LoSo == loSo);
            if (vt == null)
            {
                return Json(new
                {
                    tonTai = false,
                    message = $"Vị trí Lầu {lau} - Lô {loSo} chưa có trong hệ thống."
                });
            }

            bool isTrong = (vt.IdTinhTrang == 3); // 3 là Trống
            return Json(new
            {
                tonTai = true,
                idViTri = vt.IdviTri,
                isTrong = isTrong,
                message = isTrong ? $"Vị trí Lầu {lau} - Lô {loSo} đang TRỐNG." : $"Vị trí Lầu {lau} - Lô {loSo} ĐÃ CÓ NGƯỜI (hoặc đặt chỗ)."
            });
        }

        // 2.2. API Tìm kiếm thân nhân trong danh bạ (Tầng 3)
        [HttpGet]
        public async Task<IActionResult> TimKiemNguoiThan(string tuKhoa)
        {
            if (string.IsNullOrWhiteSpace(tuKhoa))
                return Json(new { timThay = false });

            tuKhoa = tuKhoa.Trim();
            var danhSach = await _context.NguoiThans
                .Where(n => (n.Ho + " " + n.Ten).Contains(tuKhoa) ||
                            (n.SoDienThoai != null && n.SoDienThoai.Contains(tuKhoa)) ||
                            (n.Cccd != null && n.Cccd.Contains(tuKhoa)))
                .OrderBy(n => n.IdnguoiThan)
                .Take(30)
                .Select(n => new
                {
                    id = n.IdnguoiThan,
                    hoTen = (n.Ho ?? "") + " " + (n.Ten ?? ""),
                    ho = n.Ho,
                    ten = n.Ten,
                    phapDanh = n.PhapDanh,
                    ngaySinh = n.NgaySinh,
                    cccd = n.Cccd,
                    ngayCap = n.NgayCap,
                    noiCap = n.NoiCap,
                    diaChi = n.DiaChi,
                    sdt = n.SoDienThoai,
                    ghiChu = n.GhiChu
                })
                .ToListAsync();

            return Json(new { timThay = danhSach.Any(), data = danhSach });
        }

        // 2.3. API Cấp ID Người thân kế tiếp / Tận dụng ID trống (Theo thuật toán chuẩn gốc)
        [HttpGet]
        public async Task<IActionResult> GetNextIdNguoiThan()
        {
            try
            {
                // 1. Lấy tất cả ID Người thân đang gắn với Cốt
                var idDangDungCot = await _context.Cots
                    .Where(c => c.IdnguoiThan != null && c.IdnguoiThan > 0)
                    .Select(c => c.IdnguoiThan.Value)
                    .Distinct()
                    .ToListAsync();

                // 2. Tìm ID trong bảng NguoiThan không còn ai đứng tên (người đã rút đi)
                var idDaRut = await _context.NguoiThans
                    .Where(n => !idDangDungCot.Contains(n.IdnguoiThan))
                    .OrderBy(n => n.IdnguoiThan)
                    .Select(n => n.IdnguoiThan)
                    .FirstOrDefaultAsync();

                if (idDaRut > 0)
                {
                    return Json(new
                    {
                        success = true,
                        id = idDaRut,
                        isReused = true,
                        message = $"Tái sử dụng ID trống #{idDaRut} (từ người đã rút cốt)"
                    });
                }

                // 3. Nếu không có khoảng trống, lấy ID lớn nhất + 1 (Cách an toàn không bao giờ báo lỗi đỏ)
                int maxId = 0;
                if (await _context.NguoiThans.AnyAsync())
                {
                    maxId = await _context.NguoiThans.MaxAsync(n => n.IdnguoiThan);
                }
                int nextId = maxId + 1;

                return Json(new
                {
                    success = true,
                    id = nextId,
                    isReused = false,
                    message = $"Cấp mã ID mới kế tiếp #{nextId}"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        // 2.4. POST: Cots/CreateAllInOne (Lưu trọn gói 3 Tầng vào Cơ sở dữ liệu)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> CreateAllInOne(
             Cot cot,
             IFormFile? HinhAnhUpload,
             bool TaoViTriMoi, string? VT_Lau, string? VT_LoSo,
             bool TaoNguoiThanMoi,
             int? NT_CustomId, string? NT_Ho, string? NT_Ten, string? NT_PhapDanh,
             string? NT_NgaySinh, string? NT_CCCD, string? NT_NgayCap, string? NT_NoiCap,
             string? NT_DiaChi, string? NT_SDT, string? NT_GhiChu)
        {
            try
            {
                // =======================================================
                // 1. XỬ LÝ TẦNG 2: VỊ TRÍ
                // =======================================================
                if (TaoViTriMoi && !string.IsNullOrWhiteSpace(VT_LoSo))
                {
                    var vtMoi = new ViTri
                    {
                        Lau = VT_Lau?.Trim() ?? "1",
                        LoSo = VT_LoSo.Trim(),
                        IdTinhTrang = 1 // 1: Đã có cốt
                    };
                    _context.ViTris.Add(vtMoi);
                    await _context.SaveChangesAsync();
                    cot.IdviTri = vtMoi.IdviTri;
                }
                else if (cot.IdviTri > 0)
                {
                    var vt = await _context.ViTris.FindAsync(cot.IdviTri);
                    if (vt != null)
                    {
                        vt.IdTinhTrang = 1;
                        _context.Update(vt);
                    }
                }

                // =======================================================
                // 2. XỬ LÝ TẦNG 3: NGƯỜI THÂN
                // =======================================================
                if (TaoNguoiThanMoi && !string.IsNullOrWhiteSpace(NT_Ten))
                {
                    NguoiThan ntLuu;
                    if (NT_CustomId.HasValue && NT_CustomId.Value > 0)
                    {
                        var ntCu = await _context.NguoiThans.FindAsync(NT_CustomId.Value);
                        if (ntCu != null)
                        {
                            ntLuu = ntCu; // Tận dụng dòng ID trống
                        }
                        else
                        {
                            ntLuu = new NguoiThan { IdnguoiThan = NT_CustomId.Value };
                            _context.NguoiThans.Add(ntLuu);
                        }
                    }
                    else
                    {
                        ntLuu = new NguoiThan();
                        _context.NguoiThans.Add(ntLuu);
                    }

                    ntLuu.Ho = NT_Ho?.Trim() ?? "";
                    ntLuu.Ten = NT_Ten.Trim();
                    ntLuu.PhapDanh = NT_PhapDanh?.Trim();
                    ntLuu.NgaySinh = NT_NgaySinh?.Trim();
                    ntLuu.Cccd = NT_CCCD?.Trim();
                    ntLuu.NgayCap = NT_NgayCap?.Trim();
                    ntLuu.NoiCap = NT_NoiCap?.Trim();
                    ntLuu.DiaChi = NT_DiaChi?.Trim();
                    ntLuu.SoDienThoai = NT_SDT?.Trim();
                    ntLuu.GhiChu = NT_GhiChu?.Trim();
                    ntLuu.NgayDangKy = DateTime.Today.ToString("dd/MM/yyyy");

                    await _context.SaveChangesAsync();
                    cot.IdnguoiThan = ntLuu.IdnguoiThan;
                }
                else if (cot.IdnguoiThan.HasValue && cot.IdnguoiThan.Value > 0)
                {
                    // Cập nhật thông tin nếu chọn người thân cũ
                    var ntCu = await _context.NguoiThans.FindAsync(cot.IdnguoiThan.Value);
                    if (ntCu != null)
                    {
                        if (!string.IsNullOrWhiteSpace(NT_CCCD)) ntCu.Cccd = NT_CCCD.Trim();
                        if (!string.IsNullOrWhiteSpace(NT_SDT)) ntCu.SoDienThoai = NT_SDT.Trim();
                        if (!string.IsNullOrWhiteSpace(NT_DiaChi)) ntCu.DiaChi = NT_DiaChi.Trim();
                        if (!string.IsNullOrWhiteSpace(NT_PhapDanh)) ntCu.PhapDanh = NT_PhapDanh.Trim();
                        if (!string.IsNullOrWhiteSpace(NT_NgaySinh)) ntCu.NgaySinh = NT_NgaySinh.Trim();
                        if (!string.IsNullOrWhiteSpace(NT_NgayCap)) ntCu.NgayCap = NT_NgayCap.Trim();
                        if (!string.IsNullOrWhiteSpace(NT_NoiCap)) ntCu.NoiCap = NT_NoiCap.Trim();
                        if (!string.IsNullOrWhiteSpace(NT_GhiChu)) ntCu.GhiChu = NT_GhiChu.Trim();

                        _context.Update(ntCu);
                        await _context.SaveChangesAsync();
                    }
                }
                else if (NT_CustomId.HasValue && NT_CustomId.Value > 0)
                {
                    cot.IdnguoiThan = NT_CustomId.Value;
                }

                // --- XỬ LÝ TẦNG 1: THÔNG TIN CỐT & ẢNH ---
                _context.Cots.Add(cot);
                await _context.SaveChangesAsync(); // Đã sinh xong Idcot mới

                if (HinhAnhUpload != null && HinhAnhUpload.Length > 0)
                {
                    var fileName = $"{cot.Idcot}.jpg";
                    using var stream = HinhAnhUpload.OpenReadStream();
                    var blobUrl = await _blobService.UploadAsync(stream, fileName);
                    cot.HinhNguoiMat = blobUrl;
                    _context.Update(cot);
                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = $"Tiếp nhận thành công hồ sơ cốt: {cot.Ho} {cot.Ten} (Mã #{cot.Idcot})!";

                // =========================================================================
                // TÍNH TOÁN CHÍNH XÁC SỐ TRANG CHỨA CỐT (DÙ LÀ TRANG 1 HAY TRANG 119...)
                // =========================================================================
                int pageSize = 20; // Khớp với pageSize ở hàm Index

                // Đếm xem có bao nhiêu bản ghi đứng trước hoặc bằng cốt này theo thứ tự Idcot
                int viTriDung = await _context.Cots.CountAsync(c => c.Idcot <= cot.Idcot);

                // Tính số trang đích (ví dụ: viTriDung = 2375 -> trang 119)
                int pageDich = (int)Math.Ceiling((double)viTriDung / pageSize);
                if (pageDich <= 0) pageDich = 1;

                // Chuyển hướng chính xác đến số trang đó kèm ID để View kích hoạt cuộn
                return RedirectToAction(nameof(Index), new { page = pageDich, highlight = cot.Idcot });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi lưu dữ liệu: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // ==========================================
        // 3. THÊM TỪ VỊ TRÍ (CREATE FROM VITRI)
        // ==========================================
        [Authorize]
        public async Task<IActionResult> CreateFromViTri(int idViTri)
        {
            var viTri = await _context.ViTris.FindAsync(idViTri);
            if (viTri == null) return NotFound();

            var cot = await _context.Cots.FirstOrDefaultAsync(c => c.IdviTri == idViTri);
            if (cot == null)
            {
                cot = new Cot
                {
                    IdviTri = idViTri,
                };
            }

            ViewBag.IdTinhTrang = viTri.IdTinhTrang;
            ViewBag.TinhTrangList = new SelectList(
                _context.TinhTrangs.ToList(),
                "IdTinhTrang",
                "TenTinhTrang",
                viTri.IdTinhTrang
            );

            return View("CreateFromViTri", cot);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFromViTri(Cot cot, int IdTinhTrang, IFormFile? HinhAnhUpload)
        {
            if (ModelState.IsValid)
            {
                var viTri = await _context.ViTris.FindAsync(cot.IdviTri);
                if (viTri != null)
                {
                    viTri.IdTinhTrang = IdTinhTrang;
                }

                var existingCot = await _context.Cots.FirstOrDefaultAsync(c => c.IdviTri == cot.IdviTri);

                if (existingCot != null)
                {
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
                    existingCot.LinkAnh = cot.LinkAnh;

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
                    _context.Cots.Add(cot);
                    await _context.SaveChangesAsync();

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

                int index = await _context.ViTris
                    .Where(v => v.IdviTri < cot.IdviTri)
                    .CountAsync();

                int pageSize = 20;
                int page = (index / pageSize) + 1;

                return RedirectToAction("Index", "ViTris", new { page = page, highlight = cot.IdviTri });
            }

            ViewBag.TinhTrangList = new SelectList(_context.TinhTrangs.ToList(), "IdTinhTrang", "TenTinhTrang");
            return View("CreateFromViTri", cot);
        }

        // ==========================================
        // 4. CHỈNH SỬA CỐT (EDIT)
        // ==========================================
        [Authorize]
        public async Task<IActionResult> Edit(int? id, int? idNguoiThan)
        {
            if (id == null) return NotFound();

            var cot = await _context.Cots.FindAsync(id);
            if (cot == null) return NotFound();

            ViewBag.IdNguoiThan = idNguoiThan;
            return View(cot);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Idcot,Ho,Ten,PhapDanh,NamSinh,MatAl,MatDl,Tuoi,NgayBatDau,NgayKetThuc,HinhNguoiMat,LinkAnh,IdviTri,IdnguoiThan")] Cot cot, IFormFile? HinhAnhUpload, int? idNguoiThan)
        {
            if (id != cot.Idcot) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    if (HinhAnhUpload != null && HinhAnhUpload.Length > 0)
                    {
                        var fileName = $"{cot.Idcot}.jpg";
                        using var stream = HinhAnhUpload.OpenReadStream();
                        var blobUrl = await _blobService.UploadAsync(stream, fileName);
                        cot.HinhNguoiMat = blobUrl;
                    }
                    else
                    {
                        var existing = await _context.Cots.AsNoTracking().FirstOrDefaultAsync(c => c.Idcot == id);
                        if (existing != null)
                            cot.HinhNguoiMat = existing.HinhNguoiMat;
                    }

                    var cotCu = await _context.Cots.AsNoTracking().FirstOrDefaultAsync(c => c.Idcot == id);
                    if (cotCu != null && cotCu.IdviTri != cot.IdviTri)
                    {
                        var viTriCu = await _context.ViTris.FindAsync(cotCu.IdviTri);
                        if (viTriCu != null)
                            viTriCu.IdTinhTrang = 3;

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

        // ==========================================
        // 5. CHI TIẾT & XÓA CỐT
        // ==========================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var cot = await _context.Cots
                .Include(c => c.IdViTriNavigation)
                .Include(c => c.IdnguoiThanNavigation)
                .FirstOrDefaultAsync(m => m.Idcot == id);

            if (cot == null) return NotFound();

            return View(cot);
        }

        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var cot = await _context.Cots
                .Include(c => c.IdViTriNavigation)
                .Include(c => c.IdnguoiThanNavigation)
                .FirstOrDefaultAsync(m => m.Idcot == id);

            if (cot == null) return NotFound();

            return View(cot);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var cot = await _context.Cots.FindAsync(id);
            if (cot != null)
            {
                var viTri = await _context.ViTris.FirstOrDefaultAsync(v => v.IdviTri == cot.IdviTri);

                if (viTri != null && (viTri.IdTinhTrang == 1 || viTri.IdTinhTrang == 2))
                {
                    viTri.IdTinhTrang = 3;
                    _context.ViTris.Update(viTri);
                }

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

        // ==========================================
        // 6. XUẤT CÁC TỆP EXCEL
        // ==========================================
        public IActionResult XuatKetQua(string searchString, int? namKetThuc)
        {
            var danhSach = _context.Cots
                .Include(c => c.IdViTriNavigation)
                .Include(c => c.IdnguoiThanNavigation)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                if (int.TryParse(searchString, out int id))
                {
                    danhSach = danhSach.Where(c => c.Idcot == id);
                }
                else
                {
                    danhSach = danhSach.Where(c =>
                        (c.Ho + " " + c.Ten).Contains(searchString) ||
                        (c.Ho != null && c.Ho.Contains(searchString)) ||
                        (c.Ten != null && c.Ten.Contains(searchString)) ||
                        (c.PhapDanh != null && c.PhapDanh.Contains(searchString)));
                }
            }

            if (namKetThuc.HasValue)
            {
                danhSach = danhSach.Where(c => c.NgayKetThuc != null && c.NgayKetThuc.Value.Year <= namKetThuc.Value);
            }

            var ds = danhSach
                .OrderBy(c => c.NgayKetThuc)
                .AsNoTracking()
                .Select(c => new
                {
                    IDNguoiThan = c.IdnguoiThan ?? 0,
                    TenNguoiThan = (c.IdnguoiThanNavigation != null)
                        ? (c.IdnguoiThanNavigation.Ho ?? "") + " " + (c.IdnguoiThanNavigation.Ten ?? "")
                        : "",
                    SDT = c.IdnguoiThanNavigation != null ? c.IdnguoiThanNavigation.SoDienThoai : "",
                    DiaChi = c.IdnguoiThanNavigation != null ? c.IdnguoiThanNavigation.DiaChi : "",
                    HoTenCot = (c.Ho ?? "") + " " + (c.Ten ?? ""),
                    ViTri = (c.IdViTriNavigation.Lau ?? "") + " - " + (c.IdViTriNavigation.LoSo ?? ""),
                    NgayKT = c.NgayKetThuc
                })
                .ToList();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("ThongBaoNguoiThan");
            var header = new[] { "ID Người Thân", "Tên Người Thân", "SĐT", "Địa chỉ", "Họ tên cốt", "Vị trí", "Ngày kết thúc" };
            for (int i = 0; i < header.Length; i++)
            {
                ws.Cell(1, i + 1).Value = header[i];
            }

            int row = 2;
            foreach (var item in ds)
            {
                ws.Cell(row, 1).Value = item.IDNguoiThan;
                ws.Cell(row, 2).Value = item.TenNguoiThan;
                ws.Cell(row, 3).Value = item.SDT;
                ws.Cell(row, 4).Value = item.DiaChi;
                ws.Cell(row, 5).Value = item.HoTenCot;
                ws.Cell(row, 6).Value = item.ViTri;
                ws.Cell(row, 7).Value = item.NgayKT?.ToString("dd/MM/yyyy");
                row++;
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            string fileName = $"ThongBaoNguoiThan_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [Authorize]
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

        public IActionResult XuatNguoiThan()
        {
            var danhSach = _context.NguoiThans
                .OrderBy(n => n.IdnguoiThan)
                .AsNoTracking()
                .ToList();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("NguoiThan");
            var header = new[] { "ID", "Họ", "Tên", "Pháp danh", "Ngày sinh", "CCCD", "Ngày cấp", "Nơi cấp", "Địa chỉ", "SĐT", "ngày Đk", "Chi Chú" };
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

        public IActionResult LocTheoNamKetThuc(int? nam, int? page)
        {
            if (nam == null)
            {
                ViewBag.ThongBao = "Vui lòng nhập năm cần lọc.";
                return View(new List<CotViewModel>().ToPagedList(1, 20));
            }

            var ds = _context.Cots
                .Include(c => c.IdnguoiThanNavigation)
                .Include(c => c.IdViTriNavigation)
                .Where(c => c.NgayKetThuc != null && c.NgayKetThuc.Value.Year <= nam)
                .Select(c => new CotViewModel
                {
                    IDNguoiThan = c.IdnguoiThan ?? 0,
                    TenNguoiThan = (c.IdnguoiThanNavigation != null) ? (c.IdnguoiThanNavigation.Ho ?? "") + " " + (c.IdnguoiThanNavigation.Ten ?? "") : "",
                    SDTNguoiThan = c.IdnguoiThanNavigation != null ? c.IdnguoiThanNavigation.SoDienThoai : "",
                    DiaChiNguoiThan = c.IdnguoiThanNavigation != null ? c.IdnguoiThanNavigation.DiaChi : "",
                    HoTenCot = (c.Ho ?? "") + " " + (c.Ten ?? ""),
                    ViTri = (c.IdViTriNavigation.Lau ?? "") + " - " + (c.IdViTriNavigation.LoSo ?? ""),
                    NgayKetThuc = c.NgayKetThuc
                })
                .OrderBy(c => c.NgayKetThuc)
                .ToList();

            int pageSize = 20;
            int pageNumber = page ?? 1;

            ViewBag.NamLoc = nam;
            return View(ds.ToPagedList(pageNumber, pageSize));
        }

        public IActionResult XuatExcelLoc(int nam)
        {
            var ds = _context.Cots
                .Include(c => c.IdnguoiThanNavigation)
                .Include(c => c.IdViTriNavigation)
                .Where(c => c.NgayKetThuc != null && c.NgayKetThuc.Value.Year <= nam)
                .Select(c => new CotViewModel
                {
                    IDNguoiThan = c.IdnguoiThan ?? 0,
                    TenNguoiThan = (c.IdnguoiThanNavigation != null) ? (c.IdnguoiThanNavigation.Ho ?? "") + " " + (c.IdnguoiThanNavigation.Ten ?? "") : "",
                    SDTNguoiThan = c.IdnguoiThanNavigation != null ? c.IdnguoiThanNavigation.SoDienThoai : "",
                    DiaChiNguoiThan = c.IdnguoiThanNavigation != null ? c.IdnguoiThanNavigation.DiaChi : "",
                    HoTenCot = (c.Ho ?? "") + " " + (c.Ten ?? ""),
                    ViTri = (c.IdViTriNavigation.Lau ?? "") + " - " + (c.IdViTriNavigation.LoSo ?? ""),
                    NgayKetThuc = c.NgayKetThuc
                })
                .ToList();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("DanhSachCot");

            ws.Cell(1, 1).Value = "ID Người Thân";
            ws.Cell(1, 2).Value = "Tên Người Thân";
            ws.Cell(1, 3).Value = "SĐT";
            ws.Cell(1, 4).Value = "Địa chỉ";
            ws.Cell(1, 5).Value = "Họ tên cốt";
            ws.Cell(1, 6).Value = "Vị trí";
            ws.Cell(1, 7).Value = "Ngày kết thúc";

            int row = 2;
            foreach (var item in ds)
            {
                ws.Cell(row, 1).Value = item.IDNguoiThan;
                ws.Cell(row, 2).Value = item.TenNguoiThan;
                ws.Cell(row, 3).Value = item.SDTNguoiThan;
                ws.Cell(row, 4).Value = item.DiaChiNguoiThan;
                ws.Cell(row, 5).Value = item.HoTenCot;
                ws.Cell(row, 6).Value = item.ViTri;
                ws.Cell(row, 7).Value = item.NgayKetThuc?.ToString("dd/MM/yyyy");
                row++;
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            string fileName = $"DanhSachCot_NamKetThuc_{nam}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private bool CotExists(int id)
        {
            return _context.Cots.Any(e => e.Idcot == id);
        }
    }
}