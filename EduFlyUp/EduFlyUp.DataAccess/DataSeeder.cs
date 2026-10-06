using EduFlyUp.BusinessObjects;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.DataAccess
{
    public static class DataSeeder
    {
        public static async Task SeedAsync(AppDbContext context)
        {
            await context.Database.MigrateAsync();

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
                    InstructorId = "instructor@eduflyup.vn",
                    Lessons = new List<Lesson>
                    {
                        new Lesson { Title = "Giới thiệu C#", Order = 1, DurationMinutes = 15, IsPreview = true },
                        new Lesson { Title = "Biến và kiểu dữ liệu", Order = 2, DurationMinutes = 25 },
                        new Lesson { Title = "Vòng lặp và điều kiện", Order = 3, DurationMinutes = 30 },
                    }
                },
                new Course
                {
                    Title = "ASP.NET Core Web API & Repository Pattern",
                    Description = "Xây dựng hệ thống chuyên nghiệp theo chuẩn N-Tier & Repository Pattern",
                    Price = 499000,
                    Level = CourseLevel.Intermediate,
                    IsPublished = true,
                    InstructorId = "instructor@eduflyup.vn",
                }
            };

            await context.Courses.AddRangeAsync(courses);
            await context.SaveChangesAsync();
        }

        public static async Task SeedRolesAndAdminAsync(
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager)
        {
            string[] roles = ["Admin", "Instructor", "Teacher", "Student"];
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 1. Seed Admin
            var adminEmail = "admin@eduflyup.vn";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Administrator EduFlyUp",
                    EmailConfirmed = true,
                    AvatarUrl = "https://ui-avatars.com/api/?name=Admin+EduFlyUp&background=4f46e5&color=fff"
                };

                var createResult = await userManager.CreateAsync(adminUser, "Admin@123");
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            // 2. Seed Instructor (Giảng viên)
            var instructorEmail = "instructor@eduflyup.vn";
            var instructorUser = await userManager.FindByEmailAsync(instructorEmail);

            if (instructorUser == null)
            {
                instructorUser = new ApplicationUser
                {
                    UserName = instructorEmail,
                    Email = instructorEmail,
                    FullName = "Giảng Viên EduFlyUp",
                    EmailConfirmed = true,
                    AvatarUrl = "https://ui-avatars.com/api/?name=Instructor+EduFlyUp&background=10b981&color=fff"
                };

                var createResult = await userManager.CreateAsync(instructorUser, "Instructor@123");
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(instructorUser, "Instructor");
                }
            }
        }
    }
}
