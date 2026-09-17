using EduFlyUp.Infrastructure;
using EduFlyUp.Infrastructure.Data;
using EduFlyUp.Infrastructure.Data.Seed;

var builder = WebApplication.CreateBuilder(args);

// ──────────────── Đăng ký Services ────────────────
builder.Services.AddControllersWithViews();

// Gọi extension method — đăng ký DbContext + Repositories
builder.Services.AddInfrastructure(builder.Configuration);

// ──────────────── Build App ────────────────
var app = builder.Build();

// ──────────────── Seed Data khi khởi động ────────────────
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DataSeeder.SeedAsync(context);
}

// ──────────────── Middleware Pipeline ────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
