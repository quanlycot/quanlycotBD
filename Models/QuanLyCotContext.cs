using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace QuanLyCotWeb.Models
{
    public partial class QuanLyCotContext : IdentityDbContext<IdentityUser>
    {
        public QuanLyCotContext(DbContextOptions<QuanLyCotContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Cot> Cots { get; set; }
        public virtual DbSet<NguoiThan> NguoiThans { get; set; }
        public virtual DbSet<ViTri> ViTris { get; set; }
        public virtual DbSet<TinhTrang> TinhTrangs { get; set; }
        public DbSet<RutCot> RutCot { get; set; }


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer("Server=localhost\\MSSQLSERVER01;Database=QuanLyCot;Trusted_Connection=True;TrustServerCertificate=True;");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // cần cho Identity

            // Bảng Cots
            modelBuilder.Entity<Cot>(entity =>
            {
                entity.HasKey(e => e.Idcot);
                entity.ToTable("Cot");

                entity.Property(e => e.Idcot).HasColumnName("IDCot");
                entity.Property(e => e.Ho).HasMaxLength(50);
                entity.Property(e => e.Ten).HasMaxLength(50);
                entity.Property(e => e.PhapDanh).HasMaxLength(50);
                entity.Property(e => e.NamSinh).HasMaxLength(50);
                entity.Property(e => e.MatAl).HasMaxLength(50).HasColumnName("MatAL");
                entity.Property(e => e.MatDl).HasMaxLength(50).HasColumnName("MatDL");
                entity.Property(e => e.HinhNguoiMat).HasMaxLength(100);
                entity.Property(e => e.IdviTri).HasColumnName("IDViTri");
                entity.Property(e => e.IdnguoiThan).HasColumnName("IDNguoiThan");
            });

            // Bảng NguoiThan
            modelBuilder.Entity<NguoiThan>(entity =>
            {
                entity.HasKey(e => e.IdnguoiThan);
                entity.ToTable("NguoiThan");

                entity.Property(e => e.IdnguoiThan).HasColumnName("IDNguoiThan").ValueGeneratedNever();
                entity.Property(e => e.Ho).HasMaxLength(50);
                entity.Property(e => e.Ten).HasMaxLength(50);
                entity.Property(e => e.PhapDanh).HasMaxLength(50);
                entity.Property(e => e.NgaySinh).HasMaxLength(50);
                entity.Property(e => e.Cccd).HasMaxLength(20).HasColumnName("CCCD");
                entity.Property(e => e.NoiCap).HasMaxLength(100);
                entity.Property(e => e.NgayCap).HasMaxLength(20);
                entity.Property(e => e.DiaChi).HasMaxLength(200);
                entity.Property(e => e.SoDienThoai).HasMaxLength(100);
                entity.Property(e => e.GhiChu).HasMaxLength(100);
                entity.Property(e => e.NgayDangKy).HasMaxLength(50);
            });

            // Bảng TinhTrang
            modelBuilder.Entity<TinhTrang>(entity =>
            {
                entity.HasKey(e => e.IdTinhTrang);
                entity.ToTable("TinhTrang");

                entity.Property(e => e.IdTinhTrang).HasColumnName("IDTinhTrang");
                entity.Property(e => e.TenTinhTrang).HasMaxLength(100);
            });

            // Bảng ViTri
            modelBuilder.Entity<ViTri>(entity =>
            {
                entity.HasKey(e => e.IdviTri);
                entity.ToTable("ViTri");

                entity.Property(e => e.IdviTri).HasColumnName("IDViTri");
                entity.Property(e => e.Lau).HasMaxLength(10).IsFixedLength();
                entity.Property(e => e.LoSo).HasMaxLength(10).IsFixedLength();
                entity.Property(e => e.IdTinhTrang).HasColumnName("IDTinhTrang");

                // Khóa ngoại đến bảng TinhTrang
                entity.HasOne(e => e.TinhTrangNavigation)
                      .WithMany(t => t.ViTris)
                      .HasForeignKey(e => e.IdTinhTrang)
                      .HasConstraintName("FK_ViTri_TinhTrang");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
