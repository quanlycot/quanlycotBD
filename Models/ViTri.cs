using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyCotWeb.Models
{
    [Table("ViTri")]
    public class ViTri
    {
        [Key]
        [Column("IDViTri")]
        public int IdviTri { get; set; }

        [Required]
        [StringLength(10)]
        public string Lau { get; set; }

        [Required]
        [StringLength(10)]
        public string LoSo { get; set; }

        [Column("IDTinhTrang")]
        public int? IdTinhTrang { get; set; }

        [ForeignKey("IdTinhTrang")]
        public virtual TinhTrang? TinhTrangNavigation { get; set; }

        public virtual Cot? Cot { get; set; }
    }
}
