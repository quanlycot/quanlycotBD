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
using TemplateEngine.Docx;
using System.IO;
using System.Collections.Generic;
using QuanLyCotWeb.Models;
using TemplateEngine.Docx;


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
            string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "MauDKCot", "GIAY_DK_COT.docx");

            // Tạo file tạm trong thư mục hệ thống
            string tempFile = Path.GetTempFileName();
            System.IO.File.Copy(templatePath, tempFile, true);

            // Gán nội dung vào template
            using (var outputDoc = new TemplateEngine.Docx.TemplateProcessor(tempFile).SetRemoveContentControls(true))
            {
                var valuesToFill = new Content(
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
                $"DonDangKy_{cot.Idcot}.docx");
        }

        [HttpPost]
        public IActionResult InGiayDangKyNhieuCot(List<int> selectedCotIds)
        {
            if (selectedCotIds == null || selectedCotIds.Count == 0)
                return BadRequest("Không có cốt nào được chọn.");

            var danhSachCot = _context.Cots
                .Where(c => selectedCotIds.Contains(c.Idcot))
                .Include(c => c.IdnguoiThanNavigation)
                .Include(c => c.IdViTriNavigation)
                    .ThenInclude(v => v.TinhTrangNavigation)
                .OrderBy(c => c.Idcot)
                .ToList();

            var nguoiThan = danhSachCot.First().IdnguoiThanNavigation;
            if (nguoiThan == null) return NotFound("Không tìm thấy người thân.");

            string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "MauDKCot", "GIAY_DK_COT_Full.docx");
            using var templateStream = System.IO.File.OpenRead(templatePath);
            using var memStream = new MemoryStream();
            templateStream.CopyTo(memStream);
            memStream.Position = 0;

            using (var outputDoc = new TemplateProcessor(memStream).SetRemoveContentControls(true))
            {
                var valuesToFill = new Content(
                    new FieldContent("MaSoHoSo", nguoiThan.IdnguoiThan.ToString()),
                    new FieldContent("HoTenNT", $"{nguoiThan.Ho} {nguoiThan.Ten}"),
                    new FieldContent("PhapDanhNT", nguoiThan.PhapDanh ?? ""),
                    new FieldContent("NgaySinhNT", nguoiThan.NgaySinh ?? ""),
                    new FieldContent("CCCD", nguoiThan.Cccd ?? ""),
                    new FieldContent("NgayCap", nguoiThan.NgayCap ?? ""),
                    new FieldContent("NoiCap", nguoiThan.NoiCap ?? ""),
                    new FieldContent("DiaChi", nguoiThan.DiaChi ?? ""),
                    new FieldContent("SDT", nguoiThan.SoDienThoai ?? ""),
                    new FieldContent("SoLuong", danhSachCot.Count.ToString())
                );

                var tableRows = danhSachCot.Select((cot, index) => new TableRowContent(
                    new FieldContent("STT", (index + 1).ToString()),
                    new FieldContent("HoTenCot", $"{cot.Ho} {cot.Ten}"),
                    new FieldContent("ViTri", $"{cot.IdViTriNavigation?.Lau}:{cot.IdViTriNavigation?.LoSo}"),
                    new FieldContent("TinhTrang", cot.IdViTriNavigation?.TinhTrangNavigation?.TenTinhTrang ?? ""),
                    new FieldContent("ThoiHan", $"{cot.NgayBatDau?.ToString("dd/MM/yyyy")} => {cot.NgayKetThuc?.ToString("dd/MM/yyyy")}")
                )).ToList();

                valuesToFill.Tables.Add(new TableContent("DanhSachCot", tableRows));
                outputDoc.FillContent(valuesToFill);
                outputDoc.SaveChanges();
            }

            return File(memStream.ToArray(),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                $"DonDangKyNhieuCot_{nguoiThan.IdnguoiThan}.docx");
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
            ViewBag.SoLuongCot = danhSachCot.Count;

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
        // GET: NguoiThans/Edit/1
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var nguoiThan = await _context.NguoiThans.FindAsync(id);
            if (nguoiThan == null)
                return NotFound();

            return View(nguoiThan);
        }


        // POST: NguoiThans/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, NguoiThan nguoiThan)
        {
            if (id != nguoiThan.IdnguoiThan)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(nguoiThan);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.NguoiThans.Any(e => e.IdnguoiThan == nguoiThan.IdnguoiThan))
                        return NotFound();
                    else
                        throw;
                }

                // Tính lại số trang để quay về đúng dòng vừa sửa
                int index = await _context.NguoiThans
                    .Where(n => n.IdnguoiThan < nguoiThan.IdnguoiThan)
                    .CountAsync();

                int pageSize = 20;
                int page = (index / pageSize) + 1;

                return RedirectToAction("Index", new { page = page, highlight = nguoiThan.IdnguoiThan });
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
