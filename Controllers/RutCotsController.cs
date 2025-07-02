using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyCotWeb.Helpers;
using QuanLyCotWeb.Models;
using X.PagedList;
using ClosedXML.Excel;
using System.IO;
using X.PagedList.Extensions;

namespace QuanLyCotWeb.Controllers
{
    public class RutCotsController : Controller
    {
        private readonly QuanLyCotContext _context;

        public RutCotsController(QuanLyCotContext context)
        {
            _context = context;
        }
        private int GetNextIDRut()
        {
            var usedIds = _context.RutCot
                .Where(x => x.IDRut != null)
                .Select(x => x.IDRut.Value)
                .ToList();

            for (int i = 1; i < 100000; i++)
            {
                if (!usedIds.Contains(i))
                    return i;
            }

            return usedIds.Max() + 1;
        }

        // GET: RutCots
        public async Task<IActionResult> Index(string search, int? page)
        {
            int pageSize = 20;
            int pageNumber = page ?? 1;

            var query = _context.RutCot.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                string keyword = StringHelper.NormalizeString(search.ToLower());
                string keywordRaw = search.ToLower();

                query = query.Where(x =>
                    x.IDRut.ToString().Contains(search) ||
                    (x.HoNguoiRut + " " + x.TenNguoiRut).ToLower().Contains(keywordRaw) ||
                    x.HoTenCotKhongDau.Contains(keyword));
            }

            var ds = query
                 .OrderBy(x => x.IDRut)
    .             ToPagedList(pageNumber, pageSize);

            return View(ds);

        }

        public IActionResult ExportExcel(string search)
        {
            var query = _context.RutCot.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                string keyword = StringHelper.NormalizeString(search.ToLower());
                string keywordRaw = search.ToLower();

                query = query.Where(x =>
                    x.IDRut.ToString().Contains(search) ||
                    (x.HoNguoiRut + " " + x.TenNguoiRut).ToLower().Contains(keywordRaw) ||
                    x.HoTenCotKhongDau.Contains(keyword));
            }

            var data = query.OrderBy(x => x.IDRut).ToList();

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("DanhSachRutCot");

                string[] headers = new string[]
                {
                    "IDRut", "Họ Người Rút", "Tên Người Rút", "Năm Sinh Người Rút", "CMND",
                    "Ngày Cấp", "Nơi Cấp", "Địa Chỉ", "SĐT", "Họ Tên Cốt", "Lô", "Lầu", "Ngày Rút", "Lý Do"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    ws.Cell(1, i + 1).Value = headers[i];
                    ws.Cell(1, i + 1).Style.Font.Bold = true;
                    ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                    ws.Cell(1, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                for (int i = 0; i < data.Count; i++)
                {
                    var item = data[i];

                    ws.Cell(i + 2, 1).Value = item.IDRut;
                    ws.Cell(i + 2, 2).Value = item.HoNguoiRut;
                    ws.Cell(i + 2, 3).Value = item.TenNguoiRut;
                    ws.Cell(i + 2, 4).Value = item.NamSinhNguoiRut;
                    ws.Cell(i + 2, 5).Value = item.CMND;
                    ws.Cell(i + 2, 6).Value = item.NgayCap;
                    ws.Cell(i + 2, 7).Value = item.NoiCap;
                    ws.Cell(i + 2, 8).Value = item.DiaChi;
                    ws.Cell(i + 2, 9).Value = item.SDT;

                    var hoTenCotCell = ws.Cell(i + 2, 10);
                    hoTenCotCell.Value = item.HoTenCot;
                    hoTenCotCell.Style.Alignment.WrapText = true;

                    var loCell = ws.Cell(i + 2, 11);
                    loCell.Value = item.Lo;
                    loCell.Style.Alignment.WrapText = true;

                    var lauCell = ws.Cell(i + 2, 12);
                    lauCell.Value = item.Lau;
                    lauCell.Style.Alignment.WrapText = true;

                    ws.Cell(i + 2, 13).Value = item.NgayRut;
                    ws.Cell(i + 2, 14).Value = item.LyDo;
                }

                ws.Columns().AdjustToContents();
                ws.Rows().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "DanhSachRutCot.xlsx");
                }
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            var rutCot = await _context.RutCot
                .FirstOrDefaultAsync(m => m.IDRut == id);
            if (rutCot == null)
            {
                return NotFound();
            }

            return View(rutCot);
        }

        public IActionResult Create()
        {
            ViewBag.NextID = GetNextIDRut(); // Gửi ID tiếp theo sang View
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("HoNguoiRut,TenNguoiRut,NamSinhNguoiRut,CMND,NgayCap,NoiCap,DiaChi,SDT,HoTenCot,Lo,Lau,NgayRut,LyDo")] RutCot rutCot)
        {
            if (ModelState.IsValid)
            {
                rutCot.IDRut = GetNextIDRut(); // Gán ID tự động ở đây
                rutCot.HoTenCotKhongDau = StringHelper.NormalizeString(rutCot.HoTenCot ?? ""); // Thêm nếu bạn dùng cột khongdau
                _context.Add(rutCot);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.NextID = GetNextIDRut(); // Nếu lỗi, giữ lại ID tiếp theo
            return View(rutCot);
        }


        public async Task<IActionResult> Edit(int id)
        {
            var rutCot = await _context.RutCot.FindAsync(id);
            if (rutCot == null)
            {
                return NotFound();
            }
            return View(rutCot);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IDRut,HoNguoiRut,TenNguoiRut,NamSinhNguoiRut,CMND,NgayCap,NoiCap,DiaChi,SDT,HoTenCot,Lo,Lau,NgayRut,LyDo")] RutCot rutCot)
        {
            if (id != rutCot.IDRut)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    rutCot.HoTenCotKhongDau = StringHelper.NormalizeString(rutCot.HoTenCot ?? "");
                    _context.Update(rutCot);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RutCotExists(rutCot.IDRut.Value))
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
            return View(rutCot);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var rutCot = await _context.RutCot
                .FirstOrDefaultAsync(m => m.IDRut == id);
            if (rutCot == null)
            {
                return NotFound();
            }

            return View(rutCot);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var rutCot = await _context.RutCot.FindAsync(id);
            if (rutCot != null)
            {
                _context.RutCot.Remove(rutCot);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool RutCotExists(int id)
        {
            return _context.RutCot.Any(e => e.IDRut == id);
        }
    }
}
