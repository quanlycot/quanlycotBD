using System;
using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuanLyCotWeb.Models;
using QuanLyCotWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// ==============================================================
// 1. CẤU HÌNH DỊCH VỤ (SERVICES)
// ==============================================================
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Đăng ký dịch vụ lưu trữ Azure Blob
builder.Services.AddSingleton<BlobService>();
builder.Services.AddScoped<IBlobService, BlobService>();

// Đăng ký kết nối Cơ sở dữ liệu SQL Server
builder.Services.AddDbContext<QuanLyCotContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Cấu hình ASP.NET Core Identity bảo mật
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    // Cấu hình yêu cầu độ phức tạp của mật khẩu
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;             // Bắt buộc có số
    options.Password.RequiredLength = 8;              // Tối thiểu 8 ký tự
    options.Password.RequireNonAlphanumeric = true;    // Bắt buộc có ký tự đặc biệt (@, #, $...)
    options.Password.RequireUppercase = true;         // Bắt buộc có chữ hoa
    options.Password.RequireLowercase = true;         // Bắt buộc có chữ thường

    // Khóa tài khoản nếu nhập sai liên tiếp để chống tấn công Brute-force
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddRoles<IdentityRole>() // Bắt buộc để sử dụng User.IsInRole("Admin")
.AddEntityFrameworkStores<QuanLyCotContext>();

// Cấu hình thời hạn Cookie đăng nhập
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8); // Tự đăng xuất sau 8 tiếng
    options.SlidingExpiration = true;
});

var app = builder.Build();

// ==============================================================
// 2. KHỞI TẠO DỮ LIỆU BẢO MẬT (SEED ADMIN ACCOUNT)
// ==============================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var config = services.GetRequiredService<IConfiguration>();

        // 1. Tạo Role Admin nếu chưa có
        string roleName = "Admin";
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }

        // 2. Lấy thông tin tài khoản từ appsettings.json hoặc biến môi trường (Ưu tiên bảo mật)
        string adminEmail = config["AdminAccount:Email"] ?? "buudapagoda@gmail.com";
        string adminPassword = config["AdminAccount:Password"] ?? "BuuDaPagoda@2026Secure";

        // 3. Kiểm tra và tạo tài khoản Admin nếu chưa tồn tại
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            // Identity tự động băm mật khẩu (Hash + Salt) theo chuẩn mã hóa PBKDF2 trước khi lưu vào database
            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, roleName);
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("Lỗi khởi tạo tài khoản quản trị: " + ex.Message);
    }
}

// ==============================================================
// 3. PIPELINE XỬ LÝ HTTP REQUEST
// ==============================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Thiết lập định dạng ngày tháng tiếng Việt (dd/MM/yyyy)
var cultureInfo = new CultureInfo("vi-VN");
cultureInfo.DateTimeFormat.ShortDatePattern = "dd/MM/yyyy";
cultureInfo.DateTimeFormat.DateSeparator = "/";

var supportedCultures = new[] { cultureInfo };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(cultureInfo),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=TrangTimKiem}/{id?}");

app.MapRazorPages();

// Cấu hình cổng chạy môi trường Production
if (!app.Environment.IsDevelopment())
{
    var port = Environment.GetEnvironmentVariable("PORT");
    if (!string.IsNullOrEmpty(port))
    {
        app.Urls.Add($"http://*:{port}");
    }
}

app.Run();