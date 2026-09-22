using EduFlyUp.Domain.Entities;
using EduFlyUp.Domain.Enums;
using EduFlyUp.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.Infrastructure.Data.Seed
{
    public static class DataSeeder
    {
        public static async Task SeedAsync(AppDbContext context)
        {
            // Chạy migration trước khi seed (an toàn khi deploy)
            await context.Database.MigrateAsync();

            // Chỉ seed nếu chưa có dữ liệu (tránh trùng lặp)
            if (await context.Courses.AnyAsync()) return;

            var courses = new List<Course>
            {
                new Course
                {
                    Title = "Lập trình C# từ cơ bản đến nâng cao",
                    Description = "Khóa học toàn diện về C# cho người mới bắt đầu",
                    Price = 299000,
                    Level = CourseLevel.Beginner,
                    IsPublished = true,
                    InstructorId = "seed-instructor-1",
                    Lessons = new List<Lesson>
                    {
                        new Lesson { Title = "Giới thiệu C#", Order = 1, DurationMinutes = 15, IsPreview = true },
                        new Lesson { Title = "Biến và kiểu dữ liệu", Order = 2, DurationMinutes = 25 },
                        new Lesson { Title = "Vòng lặp và điều kiện", Order = 3, DurationMinutes = 30 },
                    }
                },
                new Course
                {
                    Title = "ASP.NET Core Web API",
                    Description = "Xây dựng RESTful API chuyên nghiệp với ASP.NET Core",
                    Price = 499000,
                    Level = CourseLevel.Intermediate,
                    IsPublished = true,
                    InstructorId = "seed-instructor-1",
                }
            };

            await context.Courses.AddRangeAsync(courses);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Seed 3 Roles hệ thống và tài khoản Admin mặc định.
        /// Chỉ tạo nếu chưa tồn tại — an toàn khi chạy lại nhiều lần.
        /// </summary>
        public static async Task SeedRolesAndAdminAsync(
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager)
        {
            // ── Tạo 3 Roles ──────────────────────────────────────────
            string[] roles = ["Admin", "Teacher", "Student"];

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // ── Tạo tài khoản Admin mặc định ─────────────────────────
            const string adminEmail = "admin@eduflyup.com";
            const string adminPassword = "Admin@123456";

            var admin = await userManager.FindByEmailAsync(adminEmail);
            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Administrator",
                    IsActive = true,
                    EmailConfirmed = true // Bỏ qua bước xác thực email
                };

                var result = await userManager.CreateAsync(admin, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                }
            }
        }
    }
}
