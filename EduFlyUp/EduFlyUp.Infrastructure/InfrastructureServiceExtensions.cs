using EduFlyUp.Domain.Interfaces;
using EduFlyUp.Infrastructure.Data;
using EduFlyUp.Infrastructure.Repositories;
using EduFlyUp.Infrastructure.Services;
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
