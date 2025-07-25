using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyCotWeb.Models;
using Microsoft.AspNetCore.Authorization;
using X.PagedList;
using TemplateEngine.Docx;
using System.IO;
using X.PagedList.Mvc.Core;
using X.PagedList.Extensions;


namespace QuanLyCotWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class HT_ViTriController : Controller
    {
        private readonly QuanLyCotContext _context;

        public HT_ViTriController(QuanLyCotContext context)
        {
            _context = context;
        }

        // In giấy đăng ký theo hình thờ
        [HttpPost]
        [HttpGet]
        public IActionResult InGiayDangKyTheoHinh(int idHinh)
        {
            var hinh = _context.HT_Hinh
                .Include(h => h.NguoiThan)
                .Include(h => h.ViTri)
                .FirstOrDefault(h => h.IDHinh == idHinh);

            if (hinh == null || hinh.NguoiThan == null)
                return NotFound("Không tìm thấy hình thờ hoặc người thân.");

            string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "MauDKCOt", "GIAY_DK_HINH.docx");
            using var templateStream = System.IO.File.OpenRead(templatePath);
            using var memStream = new MemoryStream();
            templateStream.CopyTo(memStream);
            memStream.Position = 0;

            using var outputDoc = new TemplateProcessor(memStream).SetRemoveContentControls(true);

            var content = new Content(
                new FieldContent("MaSoHoSo", hinh.IDNguoiThan.ToString()),
                new FieldContent("HoTenNT", $"{hinh.NguoiThan?.Ho} {hinh.NguoiThan?.Ten}"),
                new FieldContent("PhapDanhNT", hinh.NguoiThan?.PhapDanh ?? ""),
                new FieldContent("NgaySinhNT", hinh.NguoiThan?.NamSinh?.ToString() ?? ""),
                new FieldContent("CCCD", hinh.NguoiThan?.CCCD ?? ""),
                new FieldContent("NgayCap", hinh.NguoiThan?.NgayCap ?? ""),
                new FieldContent("NoiCap", hinh.NguoiThan?.NoiCap ?? ""),
                new FieldContent("DiaChi", hinh.NguoiThan?.DiaChi ?? ""),
                new FieldContent("SDT", hinh.NguoiThan?.SoDienThoai ?? ""),
                new FieldContent("SoLuong", "1") // vì in 1 hình
            );

            var tableRow = new TableRowContent(
                new FieldContent("STT", "1"),
                new FieldContent("HoTenCot", $"{hinh.Ho} {hinh.Ten}"),
                new FieldContent("ViTri", $"{hinh.ViTri?.Tu} - {hinh.ViTri?.Day}"),
                new FieldContent("ThoiHan", $"{hinh.NgayBatDau?.ToString("dd/MM/yyyy")} => {hinh.NgayKetThuc?.ToString("dd/MM/yyyy")}")
            );

            content.Tables.Add(new TableContent("DanhSachCot", tableRow));

            outputDoc.FillContent(content);
            outputDoc.SaveChanges();

            return File(memStream.ToArray(),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                $"Giay_DK_Hinh_{hinh.IDNguoiThan}.docx");
        }


        // GET: HT_ViTri
        public async Task<IActionResult> Index(string tu, string day, int? page, int? highlight)
        {
            var query = _context.HT_ViTri
                .Include(v => v.HinhThos)
                .AsQueryable();

            if (!string.IsNullOrEmpty(tu))
                query = query.Where(v => v.Tu != null && v.Tu.ToLower() == tu.ToLower());

            if (!string.IsNullOrEmpty(day))
                query = query.Where(v => v.Day != null && v.Day.ToLower() == day.ToLower());

            int pageSize = 20;
            int pageNumber = page ?? 1;

            ViewBag.Highlight = highlight ?? 0;

            return View(query.OrderBy(v => v.Tu).ThenBy(v => v.Day).ToPagedList(pageNumber, pageSize));
        }

        // GET: HT_ViTri/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: HT_ViTri/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HT_ViTri viTri)
        {
            if (ModelState.IsValid)
            {
                _context.Add(viTri);
                await _context.SaveChangesAsync();

                // 👉 Tính vị trí đúng theo thứ tự hiển thị
                int pageSize = 20;
                var danhSach = await _context.HT_ViTri
                    .OrderBy(v => v.Tu)
                    .ThenBy(v => v.Day)
                    .ToListAsync();

                int index = danhSach.FindIndex(v => v.IDViTri == viTri.IDViTri);
                int page = (index / pageSize) + 1;

                TempData["SuccessMessage"] = "Thêm vị trí thành công!";
                return RedirectToAction("Index", new { page = page, highlight = viTri.IDViTri });
            }

            return View(viTri);
        }


        // GET: HT_ViTri/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var viTri = await _context.HT_ViTri.FindAsync(id);
            if (viTri == null) return NotFound();

            return View(viTri);
        }

        // POST: HT_ViTri/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, HT_ViTri viTri)
        {
            if (id != viTri.IDViTri) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(viTri);
                    await _context.SaveChangesAsync();

                    // 👉 Tính trang chứa vị trí đó
                    int index = await _context.HT_ViTri
                        .Where(v => v.IDViTri < viTri.IDViTri)
                        .CountAsync();

                    int pageSize = 20;
                    int page = (index / pageSize) + 1;

                    TempData["SuccessMessage"] = "Cập nhật vị trí thành công!";
                    return RedirectToAction("Index", new { page = page, highlight = viTri.IDViTri });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ViTriExists(viTri.IDViTri)) return NotFound();
                    else throw;
                }
            }

            return View(viTri);
        }


        // GET: HT_ViTri/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var viTri = await _context.HT_ViTri.FindAsync(id);
            if (viTri == null) return NotFound();

            return View(viTri);
        }

        // POST: HT_ViTri/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var viTri = await _context.HT_ViTri.FindAsync(id);
            if (viTri != null)
            {
                _context.HT_ViTri.Remove(viTri);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã xóa vị trí Tủ: {viTri.Tu}, Dãy: {viTri.Day} thành công!";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool ViTriExists(int id)
        {
            return _context.HT_ViTri.Any(e => e.IDViTri == id);
        }
    }
}
