using EduFlyUp.Infrastructure;
using EduFlyUp.Infrastructure.Data;
using EduFlyUp.Infrastructure.Data.Seed;
using EduFlyUp.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// ──────────────── Đăng ký Services ────────────────
builder.Services.AddControllersWithViews();

// Gọi extension method — đăng ký DbContext + Identity + Repositories
builder.Services.AddInfrastructure(builder.Configuration);

// ── Cấu hình Cookie Authentication ──────────────────────────────
// AddIdentity() đã set Cookie làm scheme mặc định, chỉ cần cấu hình thêm
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";              // Redirect về đây khi chưa đăng nhập
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied"; // Redirect khi không có quyền
    options.ExpireTimeSpan = TimeSpan.FromDays(7);      // Cookie hết hạn sau 7 ngày
    options.SlidingExpiration = true;                   // Gia hạn cookie khi còn hoạt động
});

// ──────────────── Build App ────────────────
var app = builder.Build();

// ──────────────── Seed Data khi khởi động ────────────────
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DataSeeder.SeedAsync(context);

    // Task 3.9: Seed Roles và Admin account
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    await DataSeeder.SeedRolesAndAdminAsync(roleManager, userManager);
}

// ──────────────── Middleware Pipeline ────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Custom Middleware đo thời gian xử lý request
app.UseMiddleware<EduFlyUp.Web.Middleware.RequestTimingMiddleware>();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// ⚠️ Thứ tự bắt buộc: Authentication TRƯỚC Authorization
// UseAuthentication: đọc cookie/token → set HttpContext.User
// UseAuthorization: kiểm tra [Authorize] dựa trên HttpContext.User
app.UseAuthentication();
app.UseAuthorization();

// Route cho phân hệ Areas (Admin)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

// Route mặc định cho Web
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
