using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyCotWeb.Models;
using Microsoft.AspNetCore.Authorization;
using X.PagedList;
using X.PagedList.Mvc.Core;
using QuanLyCotWeb.Services;
using TemplateEngine.Docx;
using System.IO;
using System.Collections.Generic;
using X.PagedList.Extensions;


namespace QuanLyCotWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ViTrisController : Controller
    {
        private readonly QuanLyCotContext _context;
        private readonly BlobService _blobService;
        public ViTrisController(QuanLyCotContext context, BlobService blobService)
        {
            _context = context;
            _blobService = blobService;
        }

        public IActionResult InGiayDangKyTheoCot(int idCot)
        {
            var cot = _context.Cots
                .Include(c => c.IdnguoiThanNavigation)
                .Include(c => c.IdViTriNavigation)
                    .ThenInclude(v => v.TinhTrangNavigation)
                .FirstOrDefault(c => c.Idcot == idCot);

            if (cot == null || cot.IdnguoiThanNavigation == null)
                return NotFound("Không tìm thấy cốt hoặc người thân tương ứng.");

            var nguoiThan = cot.IdnguoiThanNavigation;

            // Đường dẫn tới file mẫu
            string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "MauDKCOt", "GIAY_DK_COT.docx");

            // Tạo file tạm trong thư mục hệ thống
            string tempFile = Path.GetTempFileName();
            System.IO.File.Copy(templatePath, tempFile, true);

            // Gán nội dung vào template
            using (var outputDoc = new TemplateEngine.Docx.TemplateProcessor(tempFile).SetRemoveContentControls(true))
            {
                var valuesToFill = new TemplateEngine.Docx.Content(
                    new FieldContent("MaSoHoSo", nguoiThan.IdnguoiThan.ToString()),
                    new FieldContent("HoTenNT", $"{nguoiThan.Ho} {nguoiThan.Ten}"),
                    new FieldContent("PhapDanhNT", nguoiThan.PhapDanh ?? ""),
                    new FieldContent("NgaySinhNT", nguoiThan.NgaySinh ?? ""),
                    new FieldContent("CCCD", nguoiThan.Cccd ?? ""),
                    new FieldContent("NgayCap", nguoiThan.NgayCap ?? ""),
                    new FieldContent("NoiCap", nguoiThan.NoiCap ?? ""),
                    new FieldContent("DiaChi", nguoiThan.DiaChi ?? ""),
                    new FieldContent("SDT", nguoiThan.SoDienThoai ?? ""),

                    new FieldContent("HoTenNM", $"{cot.Ho} {cot.Ten}"),
                    new FieldContent("PhapDanhNM", cot.PhapDanh ?? ""),
                    new FieldContent("NamSinh", cot.NamSinh ?? ""),
                    new FieldContent("NgayMatAL", cot.MatAl ?? ""),
                    new FieldContent("NgayMatDL", cot.MatDl ?? ""),
                    new FieldContent("TuoiAL", cot.Tuoi?.ToString() ?? ""),
                    new FieldContent("Lau", cot.IdViTriNavigation?.Lau ?? ""),
                    new FieldContent("Day", cot.IdViTriNavigation?.LoSo ?? ""),
                    new FieldContent("TinhTrang", cot.IdViTriNavigation?.TinhTrangNavigation?.TenTinhTrang ?? ""),
                    new FieldContent("SoNamDK", "10"),
                    new FieldContent("NgayBatDau", cot.NgayBatDau?.ToString("dd/MM/yyyy") ?? ""),
                    new FieldContent("NgayKetThuc", cot.NgayKetThuc?.ToString("dd/MM/yyyy") ?? "")
                );

                outputDoc.FillContent(valuesToFill);
                outputDoc.SaveChanges();
            }

            // Đọc từ file tạm rồi xóa
            byte[] bytes = System.IO.File.ReadAllBytes(tempFile);
            System.IO.File.Delete(tempFile);

            return File(bytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                $"DonDangKy_{nguoiThan.IdnguoiThan}.docx");
        }

        // GET: ViTris
        public async Task<IActionResult> Index(string lau, string loSo, int? tinhTrang, bool loSoStartsWith, int? page)
        {
            var danhSach = _context.ViTris
                .Include(v => v.TinhTrangNavigation)
                .Include(v => v.Cot)
                .AsQueryable();

            if (!string.IsNullOrEmpty(lau))
                danhSach = danhSach.Where(v => v.Lau.ToLower() == lau.ToLower());

            if (!string.IsNullOrEmpty(loSo))
            {
                if (loSoStartsWith)
                    danhSach = danhSach.Where(v => v.LoSo.ToLower().StartsWith(loSo.ToLower()));
                else
                    danhSach = danhSach.Where(v => v.LoSo.ToLower() == loSo.ToLower());
            }

            if (tinhTrang.HasValue)
                danhSach = danhSach.Where(v => v.IdTinhTrang == tinhTrang.Value);

            int pageSize = 20;
            int pageNumber = page ?? 1;

            return View(danhSach.ToPagedList(pageNumber, pageSize));

        }




        // GET: ViTris/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var viTri = await _context.ViTris
                .FirstOrDefaultAsync(m => m.IdviTri == id);
            if (viTri == null)
            {
                return NotFound();
            }

            return View(viTri);
        }

        // GET: ViTris/Create
        public IActionResult Create()
        {
            ViewBag.IdTinhTrang = new SelectList(_context.TinhTrangs, "IdTinhTrang", "TenTinhTrang");
            return View();
        }

        // POST: ViTris/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ViTri viTri)
        {
            if (ModelState.IsValid)
            {
                // Thêm vào DB
                _context.Add(viTri);
                await _context.SaveChangesAsync();

                // Lấy ID vị trí vừa thêm
                int viTriMoi = viTri.IdviTri;

                // Đếm số dòng có ID nhỏ hơn để xác định trang
                int index = await _context.ViTris.CountAsync(v => v.IdviTri < viTriMoi);

                // Tính số trang dựa vào pageSize
                int pageSize = 20;
                int page = (index / pageSize) + 1;

                TempData["SuccessMessage"] = "Thêm vị trí thành công!";
                return RedirectToAction("Index", new { page = page, highlight = viTriMoi });
            }

            // Nếu ModelState không hợp lệ
            ViewBag.IdTinhTrang = new SelectList(_context.TinhTrangs, "IdTinhTrang", "TenTinhTrang", viTri.IdTinhTrang);
            return View(viTri);
        }


        // GET: ViTris/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var viTri = await _context.ViTris.FindAsync(id);
            if (viTri == null)
            {
                return NotFound();
            }
            return View(viTri);
        }

        // POST: ViTris/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdviTri,LoSo,Lau,IdtinhTrang")] ViTri viTri)
        {
            if (id != viTri.IdviTri)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(viTri);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ViTriExists(viTri.IdviTri))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(viTri);
        }

        // GET: ViTris/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var viTri = await _context.ViTris
                .FirstOrDefaultAsync(m => m.IdviTri == id);
            if (viTri == null)
            {
                return NotFound();
            }

            return View(viTri);
        }

        // POST: ViTris/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var viTri = await _context.ViTris.FindAsync(id);
            if (viTri != null)
            {
                _context.ViTris.Remove(viTri);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã Xóa Vị Trí Lầu: {viTri.Lau} Dãy {viTri.LoSo} Thành Công!.";
            }
            return RedirectToAction(nameof(Index));
        }


        private bool ViTriExists(int id)
        {
            return _context.ViTris.Any(e => e.IdviTri == id);
        }
    }
}
