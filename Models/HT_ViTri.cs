namespace QuanLyCotWeb.Models
{
    using System.ComponentModel.DataAnnotations;

    public class HT_ViTri
    {
        [Key]
        public int IDViTri { get; set; }
        public string? Tu { get; set; }
        public string? Day { get; set; }

        // ✅ 1 vị trí có thể có nhiều Hình thờ
        public virtual List<Hinh>? HinhThos { get; set; }
    }
}
