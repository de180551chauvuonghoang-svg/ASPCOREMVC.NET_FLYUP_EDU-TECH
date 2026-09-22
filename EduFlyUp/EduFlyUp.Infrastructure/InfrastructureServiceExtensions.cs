using EduFlyUp.Domain.Interfaces;
using EduFlyUp.Infrastructure.Data;
using EduFlyUp.Infrastructure.Identity;
using EduFlyUp.Infrastructure.Repositories;
using EduFlyUp.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace EduFlyUp.Infrastructure
{
    // Extension method giúp Program.cs gọn hơn: services.AddInfrastructure(config)
    public static class InfrastructureServiceExtensions
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Đăng ký DbContext với SQL Server
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection"),
                    b => b.MigrationsAssembly("EduFlyUp.Infrastructure") // Migration nằm ở Infra
                ));

            // ── Đăng ký ASP.NET Core Identity ──────────────────────────────
            // AddIdentity<TUser, TRole>: đăng ký UserManager, SignInManager, RoleManager
            // Tự động set Cookie làm Authentication scheme mặc định
            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                // Cấu hình Password
                options.Password.RequireDigit = true;           // Cần ít nhất 1 chữ số
                options.Password.RequiredLength = 6;            // Tối thiểu 6 ký tự
                options.Password.RequireUppercase = false;      // Không bắt buộc chữ hoa
                options.Password.RequireNonAlphanumeric = false; // Không bắt buộc ký tự đặc biệt

                // Cấu hình Lockout — khóa tài khoản sau nhiều lần sai
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;

                // Cấu hình User
                options.User.RequireUniqueEmail = true; // Email phải duy nhất
            })
            .AddEntityFrameworkStores<AppDbContext>() // Dùng AppDbContext để lưu Identity tables
            .AddDefaultTokenProviders();              // Token cho: reset password, confirm email

            // Đăng ký Repository và UnitOfWork
            // Scoped: mỗi HTTP request có 1 instance riêng
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<ICourseRepository, CourseRepository>();

            // Đăng ký ICurrentUser → CurrentUserService
            // AddHttpContextAccessor: cho phép inject IHttpContextAccessor vào bất kỳ class nào
            // CurrentUserService đọc User Claims từ HttpContext để biết ai đang đăng nhập
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUser, CurrentUserService>();

            return services;
        }
    }
}
