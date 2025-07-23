using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyCotWeb.Models;
using QuanLyCotWeb.Services;
using TemplateEngine.Docx;
using X.PagedList;
using X.PagedList.Extensions;

namespace QuanLyCotWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class HT_NguoiThanController : Controller
    {
        private readonly QuanLyCotContext _context;

        private readonly IBlobService _blobService;
        public HT_NguoiThanController(QuanLyCotContext context, IBlobService blobService)
        {
            _context = context;
            _blobService = blobService; // 👈 gán giá trị
        }

        // In giấy đăng ký theo hình thờ
        [HttpPost]
        public IActionResult InGiayDangKyNhieuHinh(List<int> selectedIds)
        {
            if (selectedIds == null || selectedIds.Count == 0)
                return BadRequest("Vui lòng chọn ít nhất một hình để in.");

            var danhSachHinh = _context.HT_Hinh
                .Where(h => selectedIds.Contains(h.IDHinh))
                .Include(h => h.NguoiThan)
                .Include(h => h.ViTri)
                .OrderBy(h => h.IDHinh)
                .ToList();

            var nguoiThan = danhSachHinh.First().NguoiThan;
            if (nguoiThan == null)
                return NotFound("Không tìm thấy thông tin người thân.");

            string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "MauDKCOt", "GIAY_DK_HINH.docx");
            using var templateStream = System.IO.File.OpenRead(templatePath);
            using var memStream = new MemoryStream();
            templateStream.CopyTo(memStream);
            memStream.Position = 0;

            using var outputDoc = new TemplateProcessor(memStream).SetRemoveContentControls(true);

            var content = new Content(
                new FieldContent("MaSoHoSo", nguoiThan.IDNguoiThan.ToString()),
                new FieldContent("HoTenNT", $"{nguoiThan.Ho} {nguoiThan.Ten}"),
                new FieldContent("PhapDanhNT", nguoiThan.PhapDanh ?? ""),
                new FieldContent("NgaySinhNT", nguoiThan.NamSinh?.ToString() ?? ""),
                new FieldContent("CCCD", nguoiThan.CCCD ?? ""),
                new FieldContent("NgayCap", nguoiThan.NgayCap ?? ""),
                new FieldContent("NoiCap", nguoiThan.NoiCap ?? ""),
                new FieldContent("DiaChi", nguoiThan.DiaChi ?? ""),
                new FieldContent("SDT", nguoiThan.SoDienThoai ?? ""),
                new FieldContent("SoLuong", danhSachHinh.Count.ToString())
            );

            var tableRows = danhSachHinh.Select((hinh, index) => new TableRowContent(
                new FieldContent("STT", (index + 1).ToString()),
                new FieldContent("HoTenCot", $"{hinh.Ho} {hinh.Ten}"),
                new FieldContent("ViTri", $"{hinh.ViTri?.Tu} - {hinh.ViTri?.Day}"),
                new FieldContent("ThoiHan", $"{hinh.NgayBatDau?.ToString("dd/MM/yyyy")} => {hinh.NgayKetThuc?.ToString("dd/MM/yyyy")}")
            )).ToList();

            content.Tables.Add(new TableContent("DanhSachCot", tableRows));

            outputDoc.FillContent(content);
            outputDoc.SaveChanges();

            return File(memStream.ToArray(),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                $"Giay_DK_Hinh_{nguoiThan.IDNguoiThan}.docx");
        }
        // GET: HT_NguoiThans
        public async Task<IActionResult> Index(string searchString, int? page)
        {
            int pageSize = 20;
            int pageNumber = page ?? 1;

            var danhSach = _context.HT_NguoiThan.AsQueryable();

            searchString = searchString?.Trim();

            if (!string.IsNullOrEmpty(searchString))
            {
                bool isId = int.TryParse(searchString, out int idValue);

                danhSach = danhSach.Where(nt =>
                    (isId && nt.IDNguoiThan == idValue) ||
                    nt.Ho.Contains(searchString) ||
                    nt.Ten.Contains(searchString) ||
                    (nt.Ho + " " + nt.Ten).Contains(searchString));
            }

            var pagedList = danhSach.OrderBy(nt => nt.IDNguoiThan).ToPagedList(pageNumber, pageSize);
            return View(pagedList);
        }

        // GET: HT_NguoiThans/Create
        public IActionResult Create()
        {
            var idDaDung = _context.HT_NguoiThan.Select(n => n.IDNguoiThan).ToList();
            int idMoi = 1;
            while (idDaDung.Contains(idMoi)) idMoi++;

            var nguoiThan = new HT_NguoiThan
            {
                IDNguoiThan = idMoi
            };

            return View(nguoiThan);
        }

        // POST: HT_NguoiThans/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HT_NguoiThan nguoiThan)
        {
            if (ModelState.IsValid)
            {
                _context.Add(nguoiThan);
                await _context.SaveChangesAsync();

                int pageSize = 20;
                int viTri = await _context.HT_NguoiThan.CountAsync(nt => nt.IDNguoiThan <= nguoiThan.IDNguoiThan);
                int page = (int)Math.Ceiling(viTri / (double)pageSize);

                TempData["SuccessMessage"] = "Thêm người thân thành công.";
                return RedirectToAction(nameof(Index), new { page = page, highlight = nguoiThan.IDNguoiThan });
            }
            return View(nguoiThan);
        }

        // GET: HT_NguoiThans/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var nguoiThan = await _context.HT_NguoiThan.FindAsync(id);
            if (nguoiThan == null) return NotFound();

            return View(nguoiThan);
        }

        // POST: HT_NguoiThans/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, HT_NguoiThan nguoiThan)
        {
            if (id != nguoiThan.IDNguoiThan) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(nguoiThan);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.HT_NguoiThan.Any(e => e.IDNguoiThan == nguoiThan.IDNguoiThan))
                        return NotFound();
                    else throw;
                }

                int index = await _context.HT_NguoiThan
                    .Where(n => n.IDNguoiThan < nguoiThan.IDNguoiThan)
                    .CountAsync();

                int pageSize = 20;
                int page = (index / pageSize) + 1;

                return RedirectToAction(nameof(Index), new { page = page, highlight = nguoiThan.IDNguoiThan });
            }

            return View(nguoiThan);
        }

        // POST: HT_NguoiThans/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var nguoiThan = await _context.HT_NguoiThan.FindAsync(id);
            if (nguoiThan == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy người thân.";
                return RedirectToAction(nameof(Index));
            }

            bool coLienKetHinh = await _context.HT_Hinh.AnyAsync(h => h.IDNguoiThan == id);
            if (coLienKetHinh)
            {
                TempData["ErrorMessage"] = "Người thân này đang liên kết với hình thờ, không thể xóa!";
                return RedirectToAction(nameof(Index));
            }

            _context.HT_NguoiThan.Remove(nguoiThan);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xóa người thân thành công.";
            return RedirectToAction(nameof(Index));
        }

        // GET: HT_NguoiThans/ThongKe/5
        public async Task<IActionResult> ThongKe(int id)
        {
            var nguoiThan = await _context.HT_NguoiThan.FindAsync(id);
            if (nguoiThan == null) return NotFound();

            var danhSachHinh = await _context.HT_Hinh
            .Where(h => h.IDNguoiThan == id)
            .Include(h => h.ViTri) // ✅ đúng - đây là tên navigation property
            .ToListAsync();


            ViewBag.HoTen = nguoiThan.Ho + " " + nguoiThan.Ten;
            ViewBag.IdNguoiThan = id;
            ViewBag.SoLuongHinh = danhSachHinh.Count;

            return View(danhSachHinh);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaNhieuHinh(int idNguoiThan, List<int> selectedIds)
        {
            if (selectedIds != null && selectedIds.Count > 0)
            {
                foreach (int idHinh in selectedIds)
                {
                    var hinh = await _context.HT_Hinh.FindAsync(idHinh);
                    if (hinh != null)
                    {
                        // Xóa ảnh trên Azure nếu có
                        if (!string.IsNullOrEmpty(hinh.AnhHinh))
                        {
                            var fileName = Path.GetFileName(new Uri(hinh.AnhHinh).LocalPath);
                            await _blobService.DeleteAsync(fileName);
                        }

                        // Xóa hình thờ
                        _context.HT_Hinh.Remove(hinh);
                    }
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xóa các hình thờ được chọn và ảnh kèm theo (nếu có).";
            }
            else
            {
                TempData["ErrorMessage"] = "Vui lòng chọn ít nhất một hình thờ để xóa.";
            }

            // Trở về đúng trang của Người Thân sau khi xóa
            var danhSach = await _context.HT_NguoiThan.OrderBy(n => n.IDNguoiThan).ToListAsync();

            var viTriTrongDanhSach = danhSach
                .Select((nt, index) => new { nt.IDNguoiThan, Index = index })
                .FirstOrDefault(x => x.IDNguoiThan == idNguoiThan);

            int page = 1;
            if (viTriTrongDanhSach != null)
            {
                page = (viTriTrongDanhSach.Index / 20) + 1;
            }

            return RedirectToAction("Index", "HT_NguoiThan", new { page = page, highlight = idNguoiThan });
        }

    }
}
