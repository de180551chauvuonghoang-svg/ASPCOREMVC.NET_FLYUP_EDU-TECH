using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using EduFlyUp.Services;
using EduFlyUp.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Tải biến môi trường từ file .env
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);


// Cấu hình riêng từng máy; biến môi trường và tham số dòng lệnh vẫn ưu tiên cao nhất.
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
        .AddEnvironmentVariables()
        .AddCommandLine(args);
}

// ──────────────── Đăng ký Services ────────────────
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<EduFlyUp.Web.Storage.ILessonMaterialStorage, EduFlyUp.Web.Storage.LessonMaterialStorage>();
builder.Services.AddScoped<EduFlyUp.Web.Storage.LessonFileCleanup>();
builder.Services.AddScoped<EduFlyUp.Web.Storage.ILessonVideoStorage, EduFlyUp.Web.Storage.LessonVideoStorage>();

// 1. Cấu hình DbContext kết nối Đa Nền Tảng (Tự động chuyển đổi SQL Server & PostgreSQL)
var rawConn = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? "Server=DESKTOP-IJV2BTH\\SQLEXPRESS;Database=EduFlyUpDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    // Nhận diện PostgreSQL (Railway cấp DATABASE_URL dạng postgres:// hoặc chuỗi chứa Host=...)
    if (rawConn.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        rawConn.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
        rawConn.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
        rawConn.Contains("Username=", StringComparison.OrdinalIgnoreCase) ||
        rawConn.Contains("Port=5432", StringComparison.OrdinalIgnoreCase))
    {
        var npgsqlConn = rawConn;
        if (rawConn.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            rawConn.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            // Chuyển URI postgres://user:pass@host:port/db sang connection string của Npgsql
            var uri = new Uri(rawConn);
            var userInfo = uri.UserInfo.Split(':');
            var user = userInfo[0];
            var pass = userInfo.Length > 1 ? userInfo[1] : "";
            var db = uri.AbsolutePath.TrimStart('/');
            npgsqlConn = $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};Database={db};Username={user};Password={pass};SSL Mode=Prefer;Trust Server Certificate=true;";
        }
        options.UseNpgsql(npgsqlConn);
    }
    else
    {
        // SQL Server cho môi trường Local
        options.UseSqlServer(rawConn);
    }
});


// 2. Cấu hình ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders()
.AddActiveAccountProtection();

// 3. Đăng ký Repositories & Services theo mô hình N-Tier Repository Pattern
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<ILessonRepository, LessonRepository>();
builder.Services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
builder.Services.AddScoped<IQuestionRepository, QuestionRepository>();
builder.Services.AddScoped<IQuizRepository, QuizRepository>();

builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ILessonService, LessonService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICouponRepository, CouponRepository>();
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<IInstructorStudentRepository, InstructorStudentRepository>();
builder.Services.AddScoped<IInstructorStudentService, InstructorStudentService>();
builder.Services.AddScoped<IInstructorPayoutRepository, InstructorPayoutRepository>();
builder.Services.AddScoped<IInstructorSettlementSource, UnavailableInstructorSettlementSource>();
builder.Services.AddScoped<IInstructorEarningsSource, SettlementInstructorEarningsSource>();
builder.Services.AddScoped<IInstructorPayoutService, InstructorPayoutService>();
builder.Services.AddScoped<IInstructorRevenueRepository, InstructorRevenueRepository>();
builder.Services.AddScoped<IInstructorRevenueService, InstructorRevenueService>();
builder.Services.AddScoped<IQuestionService, QuestionService>();
builder.Services.AddScoped<IQuizService, QuizService>();
builder.Services.AddScoped<ICourseAccessService, CourseAccessService>();
builder.Services.AddScoped<IAdminCourseRepository, AdminCourseRepository>();
builder.Services.AddScoped<IAdminCourseService, AdminCourseService>();
builder.Services.AddScoped<IAdminUserRepository, AdminUserRepository>();
builder.Services.AddScoped<IAdminUserStatusLock, SqlServerAdminUserStatusLock>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<IAdminDashboardRepository, AdminDashboardRepository>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();

// 4. Cấu hình Cookie Authentication & External Logins
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";              // Redirect khi chưa đăng nhập
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied"; // Redirect khi 403
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// Cấu hình Google Authentication (ưu tiên đọc từ biến môi trường / .env)
var googleClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
    ?? builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET")
    ?? builder.Configuration["Authentication:Google:ClientSecret"];

if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
        });
}


// ──────────────── Build App ────────────────
var app = builder.Build();

// ──────────────── Seed Data khi khởi động ────────────────
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
        await DataSeeder.SeedAsync(context);

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await DataSeeder.SeedRolesAndAdminAsync(roleManager, userManager);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Lỗi kết nối cơ sở dữ liệu khi khởi động ứng dụng: {Message}", ex.Message);
    }
}

// ──────────────── Middleware Pipeline ────────────────
// Hỗ trợ Reverse Proxy (Railway, Docker, Cloudflare) nhận diện HTTPS
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                       Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}


app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Route mặc định cho Web
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
