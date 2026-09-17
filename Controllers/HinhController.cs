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
                // KIỂM TRA ĐÃ CÓ NGƯỜI AN VỊ (CÓ TÊN/HỌ)
                var daCoNguoiAnVi = await _context.HT_Hinh.AnyAsync(h =>
                    h.IDViTri == viTri.IDViTri &&
                    (!string.IsNullOrWhiteSpace(h.Ten) || !string.IsNullOrWhiteSpace(h.Ho)));

                if (daCoNguoiAnVi)
                {
                    return Json(new { success = false, message = $"Vị trí Tủ {viTri.Tu} - Dãy {viTri.Day} đã có người an vị!" });
                }

                return Json(new
                {
                    success = true,
                    exists = true,
                    idViTri = viTri.IDViTri,
                    message = $"Vị trí hợp lệ: Tủ {viTri.Tu} - Dãy {viTri.Day} (Chưa có thông tin người an vị, có thể thêm mới)"
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
                    ngaySinh = n.NamSinh,
                    ngayCap = n.NgayCap, // BỔ SUNG DÒNG NÀY
                    noiCap = n.NoiCap,   // BỔ SUNG DÒNG NÀY
                    ghiChu = n.GhiChu    // BỔ SUNG DÒNG NÀY
                })
                .ToListAsync();

            return Json(ds);
        }

        // 2.3. BỔ SUNG: API Cấp ID Người thân kế tiếp / Tận dụng ID trống (QUÉT LỖ HỔNG)
        [HttpGet]
        public async Task<IActionResult> GetNextIdNguoiThan()
        {
            try
            {
                // 1. QUÉT TỪ TRÊN XUỐNG ĐỂ TÌM LỖ HỔNG (ID BỊ XÓA)
                var existingIds = await _context.HT_NguoiThan
                    .Select(n => n.IDNguoiThan)
                    .OrderBy(id => id)
                    .ToListAsync();

                int nextId = 1;
                bool foundGap = false;

                foreach (var id in existingIds)
                {
                    if (id == nextId)
                        nextId++;
                    else if (id > nextId)
                    {
                        foundGap = true;
                        break;
                    }
                }

                if (foundGap)
                {
                    return Json(new { success = true, id = nextId, isReused = true, message = $"Tái sử dụng ID trống #{nextId} (vị trí thân nhân đã bị xóa)" });
                }

                // 2. NẾU KHÔNG BỊ XÓA MẤT ID NÀO -> TÌM ID MỒ CÔI (có trong danh bạ nhưng không gắn với Hình nào)
                var idDangDung = await _context.HT_Hinh
                    .Where(h => h.IDNguoiThan != null && h.IDNguoiThan > 0)
                    .Select(h => h.IDNguoiThan.Value)
                    .Distinct()
                    .ToListAsync();

                var idDaRut = existingIds.FirstOrDefault(id => !idDangDung.Contains(id));

                if (idDaRut > 0)
                {
                    return Json(new { success = true, id = idDaRut, isReused = true, message = $"Tái sử dụng ID #{idDaRut} (từ hồ sơ đã rút hình)" });
                }

                // 3. NẾU TẤT CẢ ĐỀU KÍN VÀ ĐANG SỬ DỤNG -> CẤP ID MỚI Ở CUỐI CÙNG
                return Json(new { success = true, id = nextId, isReused = false, message = $"Cấp mã ID mới kế tiếp #{nextId}" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // 2.4. POST: Lưu toàn bộ hồ sơ 3 tầng
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> CreateAllInOne(
            Hinh hinh,
            IFormFile? HinhAnhUpload,
            bool TaoViTriMoi, string? VT_Tu, string? VT_Day,
            bool TaoNguoiThanMoi,
            int? NT_CustomId, string? NT_Ho, string? NT_Ten, string? NT_PhapDanh,
            string? NT_NgaySinh, string? NT_CCCD, string? NT_NgayCap, string? NT_NoiCap, // Đã sửa tên tham số ở đây
            string? NT_DiaChi, string? NT_SDT, string? NT_NgayDangKy, string? NT_GhiChu)
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
                    HT_NguoiThan ntLuu;
                    // Nếu được cấp Custom ID, kiểm tra tái sử dụng
                    if (NT_CustomId.HasValue && NT_CustomId.Value > 0)
                    {
                        var ntCu = await _context.HT_NguoiThan.FindAsync(NT_CustomId.Value);
                        if (ntCu != null)
                        {
                            ntLuu = ntCu; // Tận dụng dòng ID trống
                        }
                        else
                        {
                            ntLuu = new HT_NguoiThan { IDNguoiThan = NT_CustomId.Value };
                            _context.HT_NguoiThan.Add(ntLuu);
                        }
                    }
                    else
                    {
                        ntLuu = new HT_NguoiThan();
                        _context.HT_NguoiThan.Add(ntLuu);
                    }

                    ntLuu.Ho = NT_Ho?.Trim() ?? "";
                    ntLuu.Ten = NT_Ten.Trim();
                    ntLuu.PhapDanh = NT_PhapDanh?.Trim();
                    ntLuu.NamSinh = NT_NgaySinh?.Trim();  // Gán biến NT_NgaySinh
                    ntLuu.CCCD = NT_CCCD?.Trim();
                    ntLuu.DiaChi = NT_DiaChi?.Trim();
                    ntLuu.SoDienThoai = NT_SDT?.Trim();

                    ntLuu.NgayCap = NT_NgayCap?.Trim();
                    ntLuu.NoiCap = NT_NoiCap?.Trim();

                    // Gộp Ngày Đăng Ký từ View vào chung cột Ghi Chú
                    string ngayDk = NT_NgayDangKy?.Trim() ?? DateTime.Today.ToString("dd/MM/yyyy");
                    string ghiChuGoc = NT_GhiChu?.Trim() ?? "";

                    ntLuu.GhiChu = $"Ngày ĐK: {ngayDk} - {ghiChuGoc}".Trim(' ', '-');

                    await _context.SaveChangesAsync();
                    hinh.IDNguoiThan = ntLuu.IDNguoiThan;
                }
                else if (hinh.IDNguoiThan.HasValue && hinh.IDNguoiThan.Value > 0)
                {
                    // Cập nhật TẤT CẢ thông tin nếu người dùng sửa thông tin của người thân đang có
                    var ntCu = await _context.HT_NguoiThan.FindAsync(hinh.IDNguoiThan.Value);
                    if (ntCu != null)
                    {
                        // Gán đè toàn bộ dữ liệu từ Form gửi lên
                        ntCu.Ho = NT_Ho?.Trim() ?? "";
                        ntCu.Ten = NT_Ten?.Trim() ?? ntCu.Ten; // Tên bắt buộc nên giữ tên cũ nếu form gửi rỗng
                        ntCu.PhapDanh = NT_PhapDanh?.Trim();
                        ntCu.NamSinh = NT_NgaySinh?.Trim();
                        ntCu.CCCD = NT_CCCD?.Trim();
                        ntCu.NgayCap = NT_NgayCap?.Trim();
                        ntCu.NoiCap = NT_NoiCap?.Trim();
                        ntCu.SoDienThoai = NT_SDT?.Trim();
                        ntCu.DiaChi = NT_DiaChi?.Trim();

                        // Xử lý Ngày đăng ký và Ghi chú (Gộp chung)
                        string ngayDk = NT_NgayDangKy?.Trim() ?? DateTime.Today.ToString("dd/MM/yyyy");
                        string ghiChuGoc = NT_GhiChu?.Trim() ?? "";
                        ntCu.GhiChu = $"Ngày ĐK: {ngayDk} - {ghiChuGoc}".Trim(' ', '-');

                        _context.HT_NguoiThan.Update(ntCu);
                        await _context.SaveChangesAsync();
                    }
                }
                else if (NT_CustomId.HasValue && NT_CustomId.Value > 0)
                {
                    hinh.IDNguoiThan = NT_CustomId.Value;
                }

                // --- XỬ LÝ TẦNG 1: THÔNG TIN HÌNH THỜ & ẢNH ---
                var existingHinh = await _context.HT_Hinh.FirstOrDefaultAsync(h => h.IDViTri == hinh.IDViTri);

                if (existingHinh != null)
                {
                    // CẬP NHẬT (ghi đè) thông tin mới lên dòng bị trống Tên
                    existingHinh.Ho = hinh.Ho;
                    existingHinh.Ten = hinh.Ten;
                    existingHinh.PhapDanh = hinh.PhapDanh;
                    existingHinh.NamSinh = hinh.NamSinh;
                    existingHinh.Tuoi = hinh.Tuoi;
                    existingHinh.NgayBatDau = hinh.NgayBatDau;
                    existingHinh.NgayKetThuc = hinh.NgayKetThuc;
                    existingHinh.NgayMatAL = hinh.NgayMatAL;
                    existingHinh.NgayMatDL = hinh.NgayMatDL;
                    existingHinh.IDNguoiThan = hinh.IDNguoiThan;
                    existingHinh.LinkAnh = hinh.LinkAnh;

                    _context.HT_Hinh.Update(existingHinh);
                    await _context.SaveChangesAsync();

                    hinh.IDHinh = existingHinh.IDHinh;
                }
                else
                {
                    // THÊM MỚI bình thường nếu vị trí trống hoàn toàn
                    _context.HT_Hinh.Add(hinh);
                    await _context.SaveChangesAsync(); // Sinh IDHinh
                }

                if (HinhAnhUpload != null && HinhAnhUpload.Length > 0)
                {
                    var fileName = $"HT{hinh.IDHinh}.jpg";
                    using var stream = HinhAnhUpload.OpenReadStream();
                    var blobUrl = await _blobService.UploadAsync(stream, fileName);

                    // Lấy lại record vừa lưu để cập nhật link ảnh
                    var savedHinh = await _context.HT_Hinh.FindAsync(hinh.IDHinh);
                    if (savedHinh != null)
                    {
                        savedHinh.AnhHinh = blobUrl;
                        _context.HT_Hinh.Update(savedHinh);
                        await _context.SaveChangesAsync();
                    }
                }

                TempData["SuccessMessage"] = $"Tiếp nhận thành công hồ sơ hình thờ: {hinh.Ho} {hinh.Ten} (Mã #{hinh.IDHinh})!";

                // --- TÍNH TOÁN SỐ TRANG ---
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
                existing.LinkAnh = hinh.LinkAnh;

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

            // SỬ DỤNG INCLUDE ĐỂ KÉO THEO DỮ LIỆU VỊ TRÍ VÀ NGƯỜI THÂN LÊN GIAO DIỆN
            var hinh = await _context.HT_Hinh
                .Include(h => h.ViTri)
                .Include(h => h.NguoiThan)
                .FirstOrDefaultAsync(h => h.IDHinh == id);

            if (hinh == null) return NotFound();

            return View(hinh);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(
            int id,
            Hinh hinh,
            IFormFile? HinhAnhUpload,
            bool TaoNguoiThanMoi,
            int? NT_CustomId, string? NT_Ho, string? NT_Ten, string? NT_PhapDanh,
            string? NT_NgaySinh, string? NT_CCCD, string? NT_NgayCap, string? NT_NoiCap, // Đã sửa tên tham số
            string? NT_DiaChi, string? NT_SDT, string? NT_NgayDangKy, string? NT_GhiChu)
        {
            if (id != hinh.IDHinh) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // ---------------------------------------------------------
                    // 1. XỬ LÝ TẦNG 3: NGƯỜI THÂN TRƯỚC KHI LƯU HÌNH
                    // ---------------------------------------------------------
                    if (TaoNguoiThanMoi && !string.IsNullOrWhiteSpace(NT_Ten))
                    {
                        HT_NguoiThan ntLuu;
                        if (NT_CustomId.HasValue && NT_CustomId.Value > 0)
                        {
                            var ntCu = await _context.HT_NguoiThan.FindAsync(NT_CustomId.Value);
                            if (ntCu != null) ntLuu = ntCu;
                            else
                            {
                                ntLuu = new HT_NguoiThan { IDNguoiThan = NT_CustomId.Value };
                                _context.HT_NguoiThan.Add(ntLuu);
                            }
                        }
                        else
                        {
                            ntLuu = new HT_NguoiThan();
                            _context.HT_NguoiThan.Add(ntLuu);
                        }

                        ntLuu.Ho = NT_Ho?.Trim() ?? "";
                        ntLuu.Ten = NT_Ten.Trim();
                        ntLuu.PhapDanh = NT_PhapDanh?.Trim();
                        ntLuu.NamSinh = NT_NgaySinh?.Trim();
                        ntLuu.CCCD = NT_CCCD?.Trim();
                        ntLuu.DiaChi = NT_DiaChi?.Trim();
                        ntLuu.SoDienThoai = NT_SDT?.Trim();
                        ntLuu.NgayCap = NT_NgayCap?.Trim();
                        ntLuu.NoiCap = NT_NoiCap?.Trim();

                        // Gộp Ngày Đăng Ký từ View vào chung cột Ghi Chú
                        string ngayDk = NT_NgayDangKy?.Trim() ?? DateTime.Today.ToString("dd/MM/yyyy");
                        string ghiChuGoc = NT_GhiChu?.Trim() ?? "";
                        ntLuu.GhiChu = $"Ngày ĐK: {ngayDk} - {ghiChuGoc}".Trim(' ', '-');
                                                
                        await _context.SaveChangesAsync();

                        // Gán ID người thân mới tạo vào Hình Thờ
                        hinh.IDNguoiThan = ntLuu.IDNguoiThan;
                    }
                    else if (hinh.IDNguoiThan.HasValue && hinh.IDNguoiThan.Value > 0)
                    {
                        // Cập nhật TẤT CẢ thông tin nếu người dùng sửa thông tin của người thân đang có
                        var ntCu = await _context.HT_NguoiThan.FindAsync(hinh.IDNguoiThan.Value);
                        if (ntCu != null)
                        {
                            // Gán đè toàn bộ dữ liệu từ Form gửi lên
                            ntCu.Ho = NT_Ho?.Trim() ?? "";
                            ntCu.Ten = NT_Ten?.Trim() ?? ntCu.Ten; // Tên bắt buộc nên giữ tên cũ nếu form gửi rỗng
                            ntCu.PhapDanh = NT_PhapDanh?.Trim();
                            ntCu.NamSinh = NT_NgaySinh?.Trim();
                            ntCu.CCCD = NT_CCCD?.Trim();
                            ntCu.NgayCap = NT_NgayCap?.Trim();
                            ntCu.NoiCap = NT_NoiCap?.Trim();
                            ntCu.SoDienThoai = NT_SDT?.Trim();
                            ntCu.DiaChi = NT_DiaChi?.Trim();

                            // Xử lý Ngày đăng ký và Ghi chú (Gộp chung)
                            string ngayDk = NT_NgayDangKy?.Trim() ?? DateTime.Today.ToString("dd/MM/yyyy");
                            string ghiChuGoc = NT_GhiChu?.Trim() ?? "";
                            ntCu.GhiChu = $"Ngày ĐK: {ngayDk} - {ghiChuGoc}".Trim(' ', '-');

                            _context.HT_NguoiThan.Update(ntCu);
                            await _context.SaveChangesAsync();
                        }
                    }
                    else if (NT_CustomId.HasValue && NT_CustomId.Value > 0)
                    {
                        hinh.IDNguoiThan = NT_CustomId.Value;
                    }

                    // ---------------------------------------------------------
                    // 2. XỬ LÝ ẢNH & LƯU HÌNH THỜ
                    // ---------------------------------------------------------
                    if (HinhAnhUpload != null && HinhAnhUpload.Length > 0)
                    {
                        var fileName = $"HT{hinh.IDHinh}.jpg";
                        var blobUrl = await _blobService.UploadAsync(HinhAnhUpload.OpenReadStream(), fileName);
                        hinh.AnhHinh = blobUrl;
                    }
                    else
                    {
                        // Lấy lại ảnh cũ nếu không upload ảnh mới
                        var existing = await _context.HT_Hinh.AsNoTracking().FirstOrDefaultAsync(h => h.IDHinh == id);
                        if (existing != null)
                            hinh.AnhHinh = existing.AnhHinh;
                    }

                    _context.Update(hinh);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Cập nhật hình thờ thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.HT_Hinh.Any(e => e.IDHinh == id)) return NotFound();
                    throw;
                }

                // Tính toán để trở về đúng trang chứa Hình Thờ vừa sửa
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