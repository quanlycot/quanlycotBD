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

        // GET: ViTris
        public async Task<IActionResult> Index(string lau, string loSo, int? page)
        {
            var danhSach = _context.ViTris
                .Include(v => v.TinhTrangNavigation)
                .Include(v => v.Cot)
                .AsQueryable();

            if (!string.IsNullOrEmpty(lau))
                danhSach = danhSach.Where(v => v.Lau.ToLower() == lau.ToLower());

            if (!string.IsNullOrEmpty(loSo))
                danhSach = danhSach.Where(v => v.LoSo.ToLower() == loSo.ToLower());


            int pageSize = 20;
            int pageNumber = page ?? 1;

            return View(await danhSach.ToPagedListAsync(pageNumber, pageSize));
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
