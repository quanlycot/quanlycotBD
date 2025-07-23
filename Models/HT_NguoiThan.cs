using System;
using System.ComponentModel.DataAnnotations;

namespace QuanLyCotWeb.Models
{
    public class HT_NguoiThan
    {
        [Key]
        public int IDNguoiThan { get; set; }
        public string? Ho { get; set; }
        public string? Ten { get; set; }
        public string? PhapDanh { get; set; }
        public int? NamSinh { get; set; }
        public string? CCCD { get; set; }
        public string? NgayCap { get; set; }
        public string? NoiCap { get; set; }
        public string? DiaChi { get; set; }
        public string? SoDienThoai { get; set; }
        public string? GhiChu { get; set; }

        public string HoTen => $"{Ho} {Ten}";
    }
}
