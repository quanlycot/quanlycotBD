namespace QuanLyCotWeb.Models
{
    public class HinhViewModel
    {
        public int IDHinh { get; set; }
        public string? Ho { get; set; }
        public string? Ten { get; set; }
        public string? PhapDanh { get; set; }
        public int? NamSinh { get; set; }
        public string? NgayMatAL { get; set; }
        public DateTime? NgayMatDL { get; set; }
        public int? Tuoi { get; set; }
        public DateTime? NgayBatDau { get; set; }
        public DateTime? NgayKetThuc { get; set; }
        public string? AnhHinh { get; set; }
        public string? ViTriHienThi { get; set; }  // VD: Tủ A - Dãy A1
        public int? IDNguoiThan { get; set; }
       
    }
}
