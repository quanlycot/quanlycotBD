
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
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

        public IActionResult Index(string searchString, int? namKetThuc, int? page)
        {
            int pageSize = 20;
            int pageNumber = page ?? 1;

            var danhSach = _context.HT_Hinh
                .Include(h => h.ViTri)       // ✅ Đã đổi tên
                .Include(h => h.NguoiThan)   // ✅ Đã đổi tên
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
                danhSach = danhSach.Where(h => h.NgayKetThuc != null && h.NgayKetThuc.Value.Year <= namKetThuc);
            }

            return View(danhSach.ToPagedList(pageNumber, pageSize));
        }

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
        public async Task<IActionResult> CreateFromViTri(Hinh hinh, IFormFile? HinhAnhUpload)
        {
            if (!ModelState.IsValid)
                return View("CreateFromViTri", hinh);

            var existing = await _context.HT_Hinh.FirstOrDefaultAsync(h => h.IDViTri == hinh.IDViTri);

            if (existing != null)
            {
                // Cập nhật thông tin
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

                // Cập nhật ảnh giống như bên Edit
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
                await _context.SaveChangesAsync(); // có IDHinh

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

            // Điều hướng về đúng trang và vị trí
            int pageSize = 20;
            var danhSach = await _context.HT_ViTri.OrderBy(v => v.Tu).ThenBy(v => v.Day).ToListAsync();
            int index = danhSach.FindIndex(v => v.IDViTri == hinh.IDViTri);
            int page = (index / pageSize) + 1;

            return RedirectToAction("Index", "HT_ViTri", new { page = page, highlight = hinh.IDViTri });
        }


        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var hinh = await _context.HT_Hinh
            .Include(h => h.ViTri)
            .Include(h => h.NguoiThan)
            .FirstOrDefaultAsync(h => h.IDHinh == id);

            if (hinh == null) return NotFound();

            return View(hinh);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var hinh = await _context.HT_Hinh.FindAsync(id);
            if (hinh == null) return NotFound();

            return View(hinh);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Hinh hinh, IFormFile? HinhAnhUpload)
        {
            if (id != hinh.IDHinh) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Nếu có ảnh mới thì upload lên Azure
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

                    _context.Update(hinh);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Cập nhật hình thờ thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.HT_Hinh.Any(e => e.IDHinh == id)) return NotFound();
                    throw;
                }

                // Tính thứ tự dòng để biết trang
                int index = await _context.HT_Hinh
                    .Where(h => h.IDHinh < hinh.IDHinh)
                    .CountAsync();

                int pageSize = 20;
                int page = (index / pageSize) + 1;

                return RedirectToAction("Index", new { page = page, highlight = hinh.IDHinh });

            }

            return View(hinh);
        }

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
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var hinh = await _context.HT_Hinh.FindAsync(id);
            if (hinh != null)
            {
                if (!string.IsNullOrEmpty(hinh.AnhHinh))
                {
                    var fileName = Path.GetFileName(new Uri(hinh.AnhHinh).LocalPath);
                    await _blobService.DeleteAsync(fileName);
                }

                _context.HT_Hinh.Remove(hinh);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Đã xóa hình thờ thành công.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
