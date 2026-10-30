using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using EduFlyUp.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace EduFlyUp.Tests;

public sealed class InstructorSummaryTests
{
    [Fact]
    public async Task Studio_summary_is_personal_unaffected_by_course_filters_and_has_an_empty_state()
    {
        using var app = new TestApplication();
        var owner = await app.CreateUserAsync("owner@example.test", "Instructor");
        await app.CreateUserAsync("empty@example.test", "Instructor");
        await app.CreateUserAsync("admin@example.test", "Admin");
        var first = new Course { Id = 1, Title = "Owned", InstructorId = owner };
        var second = new Course { Id = 2, Title = "Legacy", InstructorId = "owner@example.test" };
        var foreign = new Course { Id = 3, Title = "FOREIGN_COURSE", InstructorId = "foreign" };
        app.Courses.Courses.AddRange([first, second, foreign]);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Courses.AddRange(first, second, foreign);
            db.Enrollments.AddRange(new Enrollment { CourseId = 1, UserId = "student" },
                new Enrollment { CourseId = 2, UserId = "student", Status = EnrollmentStatus.Completed },
                new Enrollment { CourseId = 2, UserId = "missing", Status = EnrollmentStatus.Cancelled },
                new Enrollment { CourseId = 3, UserId = "private" });
            await db.SaveChangesAsync();
        }
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "owner@example.test");
        foreach (var path in new[] { "/Instructor/Dashboard", "/Instructor/Dashboard?search=nomatch&level=Advanced&category=none&status=Draft" })
        {
            var response = await browser.Client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("id=\"total-enrollments\">3<", html);
            Assert.Contains("id=\"unique-students\">2<", html);
            foreach (var id in new[] { "active", "completed", "cancelled" }) Assert.Contains($"id=\"{id}-enrollments\">1<", html);
            Assert.DoesNotContain("FOREIGN_COURSE", html);
            Assert.Contains("/InstructorStudents/Courses", html);
        }
        using var empty = app.CreateBrowser(); await TestApplication.LoginAsync(empty, "empty@example.test");
        var emptyHtml = await empty.Client.GetStringAsync("/Instructor/Dashboard");
        Assert.Contains("id=\"total-enrollments\">0<", emptyHtml);
        Assert.Contains("Chưa có lượt ghi danh", WebUtility.HtmlDecode(emptyHtml));
        using var admin = app.CreateBrowser(); await TestApplication.LoginAsync(admin, "admin@example.test");
        Assert.DoesNotContain("id=\"enrollment-summary\"", await admin.Client.GetStringAsync("/Instructor/Dashboard"));
    }

    [SqlServerFact]
    public async Task Summary_counts_owned_enrollments_and_distinct_students_across_courses_at_sql_server()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = new Course { Title = "Owned by ID", InstructorId = "owner-id" };
        var second = new Course { Title = "Owned by legacy email", InstructorId = "owner@example.test", Status = CourseStatus.Archived };
        var foreign = new Course { Title = "Foreign", InstructorId = "someone-else" };
        db.Enrollments.AddRange(
            new Enrollment { Course = first, UserId = "same-student", Status = EnrollmentStatus.Active },
            new Enrollment { Course = second, UserId = "same-student", Status = EnrollmentStatus.Completed },
            new Enrollment { Course = second, UserId = "missing-profile", Status = EnrollmentStatus.Cancelled },
            new Enrollment { Course = foreign, UserId = "foreign-student" });
        await db.SaveChangesAsync();
        var service = new InstructorStudentService(new InstructorStudentRepository(db));
        var summary = await service.SummaryAsync(new("owner-id", "owner@example.test"));
        Assert.Equal(3, summary.TotalEnrollments);
        Assert.Equal(2, summary.UniqueStudents);
        Assert.Equal(1, summary.ActiveEnrollments);
        Assert.Equal(1, summary.CompletedEnrollments);
        Assert.Equal(1, summary.CancelledEnrollments);
        Assert.Equal(summary.TotalEnrollments, summary.ActiveEnrollments + summary.CompletedEnrollments + summary.CancelledEnrollments);
        Assert.Equal(new InstructorEnrollmentSummary(0, 0), await service.SummaryAsync(new("empty", "empty@example.test")));
        Assert.Equal(new InstructorEnrollmentSummary(0, 0), await service.SummaryAsync(new("", "owner@example.test")));
    }
}
