using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyCotWeb.Models
{
    [Table("TinhTrang")]
    public class TinhTrang
    {
        [Key]
        [Column("IDTinhTrang")]
        public int IdTinhTrang { get; set; }

        [Required]
        [Column("TenTinhTrang")]
        [StringLength(100)]
        public string TenTinhTrang { get; set; }

        // Navigation: 1 tình trạng có nhiều vị trí
        public virtual ICollection<ViTri> ViTris { get; set; } = new List<ViTri>();
    }
}
