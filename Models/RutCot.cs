using System.ComponentModel.DataAnnotations;

public class RutCot
{
    [Key]
    public int? IDRut { get; set; }

    public string? HoNguoiRut { get; set; }
    public string? TenNguoiRut { get; set; }
    public string? NamSinhNguoiRut { get; set; }
    public string? CMND { get; set; }
    public string? NgayCap { get; set; }
    public string? NoiCap { get; set; }
    public string? DiaChi { get; set; }
    public string? SDT { get; set; }
    public string? HoTenCot { get; set; }
    public string? Lo { get; set; }
    public string? Lau { get; set; }
    public string? NgayRut { get; set; }
    public string? LyDo { get; set; }
    public string? HoTenCotKhongDau {  get; set; }
}
