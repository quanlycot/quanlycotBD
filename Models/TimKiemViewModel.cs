namespace QuanLyCotWeb.Models
{
    public class TimKiemViewModel
    {
        public string Loai { get; set; } = ""; // "Cốt" hoặc "Hình"
        public int ID { get; set; }
        public string Ho { get; set; } = "";
        public string Ten { get; set; } = "";
        public string? PhapDanh { get; set; }
        public string? NamSinh { get; set; }
        public string? NgayMatDL { get; set; }
        public int? Tuoi { get; set; }
        public string? ViTriHienThi { get; set; } // VD: "Lầu 1 - Dãy A1" hoặc "Tủ A - Dãy A1"
        public string? TenNguoiThan { get; set; }
        public string? AnhUrl { get; set; }
    }
}
