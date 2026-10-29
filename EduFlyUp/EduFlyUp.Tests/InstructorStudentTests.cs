using System.Net;
using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlyUp.Tests;

public sealed class InstructorStudentTests
{
    [Theory]
    [InlineData("Instructor", HttpStatusCode.OK)]
    [InlineData("Student", HttpStatusCode.Redirect)]
    [InlineData("Teacher", HttpStatusCode.Redirect)]
    [InlineData("Admin", HttpStatusCode.Redirect)]
    public async Task Only_instructors_can_open_student_reporting(string role, HttpStatusCode expected)
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("owner@example.test", role);
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "owner@example.test");
        Assert.Equal(expected, (await browser.Client.GetAsync("/InstructorStudents/Courses")).StatusCode);
        using var guest = app.CreateBrowser();
        Assert.Equal(HttpStatusCode.Redirect, (await guest.Client.GetAsync("/InstructorStudents/Courses")).StatusCode);
    }

    [Fact]
    public async Task Roster_is_scoped_and_does_not_disclose_authentication_data_or_raw_html()
    {
        using var app = new TestApplication();
        var owner = await app.CreateUserAsync("owner@example.test", "Instructor");
        var studentId = await app.CreateUserAsync("student@example.test", "Student");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Courses.AddRange(new Course { Id = 1, Title = "My course", InstructorId = owner },
                new Course { Id = 2, Title = "FOREIGN_COURSE", InstructorId = "other" });
            var student = await db.Users.FindAsync(studentId);
            student!.FullName = "<script>alert(1)</script>";
            student.PasswordHash = "PRIVATE_PASSWORD_HASH";
            db.Enrollments.AddRange(new Enrollment { CourseId = 1, UserId = studentId, Status = EnrollmentStatus.Completed },
                new Enrollment { CourseId = 2, UserId = studentId });
            await db.SaveChangesAsync();
        }
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "owner@example.test");
        var courses = await browser.Client.GetStringAsync("/InstructorStudents/Courses");
        Assert.Contains("My course", courses); Assert.DoesNotContain("FOREIGN_COURSE", courses);
        var response = await browser.Client.GetAsync("/InstructorStudents?courseId=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("student@example.test", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.DoesNotContain("PRIVATE_PASSWORD_HASH", html);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.Client.GetAsync("/InstructorStudents?courseId=2")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.Client.GetAsync("/InstructorStudents?courseId=999")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Client.GetAsync("/InstructorStudents?courseId=wrong")).StatusCode);
    }

    [SqlServerFact]
    public async Task Sql_roster_scopes_ownership_joins_profiles_and_pages_without_losing_missing_users()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mine = new Course { Title = "Owned", InstructorId = "owner@example.test" };
        var empty = new Course { Title = "Empty", InstructorId = "owner-id" };
        var other = new Course { Title = "Foreign", InstructorId = "other" };
        db.Courses.AddRange(mine, empty, other);
        for (var i = 0; i < 12; i++)
        {
            var id = "student-" + i;
            db.Users.Add(new ApplicationUser { Id = id, FullName = "Student " + i, Email = id + "@example.test" });
            db.Enrollments.Add(new Enrollment { UserId = id, Course = mine, Status = (EnrollmentStatus)(i % 3 + 1) });
        }
        db.Enrollments.Add(new Enrollment { UserId = "deleted-user", Course = mine });
        await db.SaveChangesAsync();
        var repository = new InstructorStudentRepository(db);
        var actor = new InstructorIdentity("owner-id", "owner@example.test");
        var courses = await repository.CoursesAsync(actor);
        Assert.Equal(2, courses.Count); Assert.Equal(13, courses.Single(c => c.Id == mine.Id).EnrollmentCount);
        var first = await repository.RosterAsync(actor, mine.Id, 1);
        Assert.NotNull(first); Assert.Equal(13, first.TotalCount); Assert.Equal(10, first.Students.Count);
        var last = await repository.RosterAsync(actor, mine.Id, int.MaxValue);
        Assert.NotNull(last); Assert.Equal(2, last.Page); Assert.Equal(3, last.Students.Count);
        Assert.Contains(first.Students.Concat(last.Students), s => s.UserId == "deleted-user" && s.Email == null);
        Assert.Equal(3, first.Students.Concat(last.Students).Select(s => s.Status).Distinct().Count());
        Assert.Null(await repository.RosterAsync(actor, other.Id, 1));
        var noStudents = await repository.RosterAsync(actor, empty.Id, 1);
        Assert.NotNull(noStudents); Assert.Empty(noStudents.Students);
        var searched = await repository.RosterAsync(actor, mine.Id, 1, "STUDENT-1");
        Assert.NotNull(searched); Assert.Equal(3, searched.TotalCount);
        var filtered = await repository.RosterAsync(actor, mine.Id, int.MaxValue, "STUDENT", EnrollmentStatus.Cancelled);
        Assert.NotNull(filtered); Assert.Equal(4, filtered.TotalCount); Assert.Equal(1, filtered.Page);
        Assert.All(filtered.Students, s => Assert.Equal(EnrollmentStatus.Cancelled, s.Status));
    }

    [Theory]
    [InlineData("status=999")]
    [InlineData("status=wrong")]
    [InlineData("page=wrong")]
    public async Task Invalid_filter_values_are_rejected(string query)
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "owner@example.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Client.GetAsync("/InstructorStudents?courseId=1&" + query)).StatusCode);
    }
}
