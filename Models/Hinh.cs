using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyCotWeb.Models
{
    public class Hinh
    {
        [Key]
        public int IDHinh { get; set; }

        public string? Ho { get; set; }
        public string? Ten { get; set; }
        public string? PhapDanh { get; set; }
        public int? NamSinh { get; set; } // ✅ Đúng với kiểu int trong SQL Server

        public string? NgayMatAL { get; set; }
        public DateTime? NgayMatDL { get; set; }
        public int? Tuoi { get; set; }

        public DateTime? NgayBatDau { get; set; }
        public DateTime? NgayKetThuc { get; set; }
                
        public int? IDNguoiThan { get; set; }

        public string? AnhHinh { get; set; }

        // ✅ Navigation properties (Đặt tên ngắn gọn, rõ ràng)
        public int? IDViTri { get; set; }

        [ForeignKey("IDViTri")]
        public virtual HT_ViTri? ViTri { get; set; }


        [ForeignKey("IDNguoiThan")]
        public virtual HT_NguoiThan? NguoiThan { get; set; }
    }
}
