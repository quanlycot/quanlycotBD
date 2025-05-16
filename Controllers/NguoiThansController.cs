using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyCotWeb.Models;
using QuanLyCotWeb.Services;
using X.PagedList;
using X.PagedList.Mvc.Core;


namespace QuanLyCotWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class NguoiThansController : Controller
    {
        private readonly QuanLyCotContext _context;
        private readonly BlobService _blobService;

        public NguoiThansController(QuanLyCotContext context, BlobService blobService)
        {
            _context = context;
            _blobService = blobService;
        }

        // GET: NguoiThans
        public async Task<IActionResult> Index(string searchString, int? page)
        {
            int pageSize = 20;
            int pageNumber = page ?? 1;

            var danhSach = _context.NguoiThans.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                bool isId = int.TryParse(searchString, out int idValue);

                danhSach = danhSach.Where(nt =>
                    (isId && nt.IdnguoiThan == idValue) ||
                    nt.Ho.Contains(searchString) ||
                    nt.Ten.Contains(searchString) ||
                    (nt.Ho + " " + nt.Ten).Contains(searchString));
            }


            var pagedList = await danhSach.OrderBy(nt => nt.IdnguoiThan).ToPagedListAsync(pageNumber, pageSize);
            return View(pagedList);
        }




        // GET: NguoiThans/ThongKe/5
        public async Task<IActionResult> ThongKe(int id)
        {
            var nguoiThan = await _context.NguoiThans.FindAsync(id);
            if (nguoiThan == null)
            {
                return NotFound();
            }

            var danhSachCot = await _context.Cots
                .Where(c => c.IdnguoiThan == id)
                .Include(c => c.IdViTriNavigation)
                .ToListAsync();

            ViewBag.HoTen = nguoiThan.Ho + " " + nguoiThan.Ten;
            ViewBag.IdNguoiThan = id;

            return View(danhSachCot);
        }

        // POST: NguoiThans/Thống Kê
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaNhieuCot(int idNguoiThan, List<int> selectedIds)
        {
            if (selectedIds != null && selectedIds.Count > 0)
            {
                foreach (int idCot in selectedIds)
                {
                    var cot = await _context.Cots.FindAsync(idCot);
                    if (cot != null)
                    {
                        // Tìm vị trí chứa cốt
                        var viTri = await _context.ViTris.FirstOrDefaultAsync(v => v.IdviTri == cot.IdviTri);

                        // Nếu vị trí có tình trạng là "Đã Có Cốt" thì cập nhật thành "Trống"
                        if (viTri != null && (viTri.IdTinhTrang == 1 || viTri.IdTinhTrang == 2))
                        {
                            viTri.IdTinhTrang = 3;
                            _context.ViTris.Update(viTri);
                        }
                        // 2. Xóa ảnh trên Azure nếu có đường link
                        if (!string.IsNullOrEmpty(cot.HinhNguoiMat))
                        {
                            var fileName = Path.GetFileName(new Uri(cot.HinhNguoiMat).LocalPath);
                            await _blobService.DeleteAsync(fileName);
                        }


                        // 3. Xóa dữ liệu cốt
                        _context.Cots.Remove(cot);
                    }
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xóa các cốt được chọn và ảnh kèm theo (nếu có).";
            }
            else
            {
                TempData["ErrorMessage"] = "Vui lòng chọn ít nhất một cốt để xóa.";
            }

            // Tìm vị trí người thân trong danh sách để xác định trang
            var danhSach = await _context.NguoiThans.OrderBy(n => n.IdnguoiThan).ToListAsync();

            var viTriTrongDanhSach = danhSach
                .Select((nt, index) => new { nt.IdnguoiThan, Index = index })
                .FirstOrDefault(x => x.IdnguoiThan == idNguoiThan);


            int page = 1;
            if (viTriTrongDanhSach != null)
            {
                page = (viTriTrongDanhSach.Index / 20) + 1;
            }
            return RedirectToAction("Index", "NguoiThans", new { page = page, highlight = idNguoiThan });

        }





        // GET: NguoiThans/Thêm Mới
        public IActionResult Create()
        {
            // Lấy danh sách ID đã dùng trong bảng
            var idDaDung = _context.NguoiThans.Select(n => n.IdnguoiThan).ToList();

            // Tìm ID nhỏ nhất chưa dùng
            int idMoi = 1;
            while (idDaDung.Contains(idMoi))
            {
                idMoi++;
            }

            // Tạo đối tượng với ID mới
            var nguoiThan = new NguoiThan
            {
                IdnguoiThan = idMoi
            };

            return View(nguoiThan);
        }


        // POST: NguoiThans/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdnguoiThan,Ho,Ten,PhapDanh,NgaySinh,Cccd,NgayCap,NoiCap,DiaChi,SoDienThoai,NgayDangKy,GhiChu")] NguoiThan nguoiThan)
        {


            if (ModelState.IsValid)
            {

                _context.Add(nguoiThan);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thêm người thân thành công.";
                int pageSize = 20;
                int viTri = await _context.NguoiThans.CountAsync(nt => nt.IdnguoiThan <= nguoiThan.IdnguoiThan);
                int page = (int)Math.Ceiling(viTri / (double)pageSize);

                return RedirectToAction(nameof(Index), new { page = page, highlight = nguoiThan.IdnguoiThan });
           
            }
            return View(nguoiThan);
        }

         
        // POST: NguoiThans/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdnguoiThan,Ho,Ten,PhapDanh,NgaySinh,Cccd,NgayCap,NoiCap,DiaChi,SoDienThoai,NgayDangKy,GhiChu")] NguoiThan nguoiThan)
        {
            if (id != nguoiThan.IdnguoiThan)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(nguoiThan);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!NguoiThanExists(nguoiThan.IdnguoiThan))
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
            return View(nguoiThan);
        }

        
        // POST: NguoiThans/Delete/5
        // POST: NguoiThans/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var nguoiThan = await _context.NguoiThans.FindAsync(id);

            if (nguoiThan == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy người thân.";
                return RedirectToAction(nameof(Index));
            }

            // Kiểm tra có liên kết với bảng Cots không
            bool coLienKetCot = await _context.Cots.AnyAsync(c => c.IdnguoiThan == id);

            if (coLienKetCot)
            {
                TempData["ErrorMessage"] = "Người thân này đang liên kết với cốt, không thể xóa!.";
                return RedirectToAction(nameof(Index));
            }

            _context.NguoiThans.Remove(nguoiThan);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xóa người thân thành công.";
            return RedirectToAction(nameof(Index));
        }


        private bool NguoiThanExists(int id)
        {
            return _context.NguoiThans.Any(e => e.IdnguoiThan == id);
        }
        private int LayIDNguoiThanTiepTheo()
        {
            var danhSachID = _context.NguoiThans
                .Select(nt => nt.IdnguoiThan)
                .OrderBy(id => id)
                .ToList();

            int id = 1;
            foreach (var idHienTai in danhSachID)
            {
                if (idHienTai != id)
                    return id;
                id++;
            }

            return id; // nếu không có ID trống thì trả về ID tiếp theo
        }

    }
}
