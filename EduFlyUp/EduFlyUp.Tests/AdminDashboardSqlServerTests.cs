using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using EduFlyUp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlyUp.Tests;

public sealed class AdminDashboardSqlServerTests
{
    [SqlServerFact]
    public async Task Sql_aggregates_handle_empty_data_and_follow_committed_admin_actions()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        using (var empty = database.Services.CreateScope())
            Assert.Equal(new AdminDashboardStats(0, 0, 0, 0, 0, 0, 0),
                await empty.ServiceProvider.GetRequiredService<IAdminDashboardService>().GetStatsAsync());

        var admin = await database.CreateUserAsync("admin@example.test");
        var student = await database.CreateUserAsync("student@example.test", "Student");
        int publishedId;
        using (var seed = database.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AppDbContext>();
            var course = new Course { Title = "Published", IsPublished = true, InstructorId = "owner@example.test",
                Lessons = [new Lesson { Title = "One" }, new Lesson { Title = "Two" }] };
            db.Courses.AddRange(course, new Course { Title = "Hidden", InstructorId = "owner@example.test",
                Lessons = [new Lesson { Title = "Three" }] });
            await db.SaveChangesAsync();
            publishedId = course.Id;
        }
        var expected = new AdminDashboardStats(2, 2, 0, 2, 1, 1, 3);
        using (var before = database.Services.CreateScope())
            Assert.Equal(expected, await before.ServiceProvider.GetRequiredService<IAdminDashboardService>().GetStatsAsync());

        foreach (var active in new[] { false, true })
        {
            using (var action = database.Services.CreateScope())
            {
                Assert.Equal(AdminUserChangeResult.Changed,
                    await action.ServiceProvider.GetRequiredService<IAdminUserService>().SetActiveAsync(admin, student, active));
                Assert.True(await new AdminCourseRepository(action.ServiceProvider.GetRequiredService<AppDbContext>())
                    .SetPublicationAsync(publishedId, active));
            }
            using var check = database.Services.CreateScope();
            Assert.Equal(active ? expected : new AdminDashboardStats(2, 1, 1, 2, 0, 2, 3),
                await check.ServiceProvider.GetRequiredService<IAdminDashboardService>().GetStatsAsync());
        }
    }
}
