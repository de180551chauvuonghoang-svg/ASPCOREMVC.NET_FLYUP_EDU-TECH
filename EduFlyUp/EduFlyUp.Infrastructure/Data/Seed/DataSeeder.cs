using EduFlyUp.Domain.Entities;
using EduFlyUp.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
