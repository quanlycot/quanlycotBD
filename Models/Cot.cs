using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace QuanLyCotWeb.Models;

public partial class Cot
{
    public int Idcot { get; set; }

    public string? Ho { get; set; }

    public string? Ten { get; set; }

    public string? PhapDanh { get; set; }

    public string? NamSinh { get; set; }

    public string? MatAl { get; set; }

    public string? MatDl { get; set; }

    public int? Tuoi { get; set; }

    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}", ApplyFormatInEditMode = true)]
    public DateTime? NgayBatDau { get; set; }

    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}", ApplyFormatInEditMode = true)]
    public DateTime? NgayKetThuc { get; set; }

   // public DateOnly? NgayBatDau { get; set; }

   // public DateOnly? NgayKetThuc { get; set; }

    public string? HinhNguoiMat { get; set; }

    public int? IdviTri { get; set; }

    public int? IdnguoiThan { get; set; }

    [ForeignKey("IdviTri")]
    public virtual ViTri? IdViTriNavigation { get; set; }  // thêm dấu hỏi ?

 
    [ForeignKey("IdnguoiThan")]
    public virtual NguoiThan? IdnguoiThanNavigation { get; set; } // thêm dấu hỏi ?

    [NotMapped]
    public int? IdTinhTrang { get; set; }

}
