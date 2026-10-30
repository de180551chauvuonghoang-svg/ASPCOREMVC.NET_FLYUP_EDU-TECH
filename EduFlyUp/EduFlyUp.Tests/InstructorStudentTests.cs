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
        if (role != "Instructor") Assert.Equal(HttpStatusCode.Redirect, (await browser.Client.GetAsync("/InstructorStudents/Details/1?courseId=1")).StatusCode);
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
        var export = await repository.ExportAsync(actor, mine.Id, null, null);
        Assert.NotNull(export); Assert.Equal(13, export.Roster.Students.Count);
        Assert.Null(await repository.ExportAsync(actor, other.Id, null, null));
        var filteredExport = await repository.ExportAsync(actor, mine.Id, "STUDENT", EnrollmentStatus.Cancelled);
        Assert.NotNull(filteredExport); Assert.Equal(4, filteredExport.Roster.Students.Count);
        Assert.Empty((await repository.ExportAsync(actor, empty.Id, null, null))!.Roster.Students);
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

    [Fact]
    public async Task Detail_only_shows_matching_student_and_course_and_hides_unsubmitted_scores()
    {
        using var app = new TestApplication();
        var owner = await app.CreateUserAsync("owner@example.test", "Instructor");
        var student = await app.CreateUserAsync("student@example.test", "Student");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Courses.AddRange(new Course { Id = 1, Title = "Mine", InstructorId = owner },
                new Course { Id = 2, Title = "Other owned", InstructorId = owner }, new Course { Id = 3, Title = "Foreign", InstructorId = "foreign" });
            db.Enrollments.AddRange(new Enrollment { Id = 1, CourseId = 1, UserId = student },
                new Enrollment { Id = 2, CourseId = 2, UserId = student }, new Enrollment { Id = 3, CourseId = 3, UserId = student });
            db.Quizzes.AddRange(new Quiz { Id = 1, CourseId = 1, Title = "VISIBLE_QUIZ" },
                new Quiz { Id = 2, CourseId = 2, Title = "HIDDEN_COURSE_QUIZ" });
            db.QuizAttempts.AddRange(new QuizAttempt { QuizId = 1, UserId = student, Score = 85.5m, Passed = true, CompletedAt = DateTime.UtcNow },
                new QuizAttempt { QuizId = 1, UserId = student, Score = 999.99m, Passed = true },
                new QuizAttempt { QuizId = 2, UserId = student, Score = 643.21m, CompletedAt = DateTime.UtcNow },
                new QuizAttempt { QuizId = 1, UserId = "different-user", Score = 123.45m, CompletedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "owner@example.test");
        var response = await browser.Client.GetAsync("/InstructorStudents/Details/1?courseId=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("VISIBLE_QUIZ", html); Assert.Contains("85,5", html);
        Assert.DoesNotContain("HIDDEN_COURSE_QUIZ", html); Assert.DoesNotContain("999,99", html); Assert.DoesNotContain("123,45", html);
        Assert.Contains("Chưa có dữ liệu", WebUtility.HtmlDecode(html));
        Assert.True(response.Headers.CacheControl?.NoStore);
        foreach (var path in new[] { "1?courseId=2", "2?courseId=1", "3?courseId=3", "999?courseId=1" })
            Assert.Equal(HttpStatusCode.NotFound, (await browser.Client.GetAsync("/InstructorStudents/Details/" + path)).StatusCode);
    }

    [SqlServerFact]
    public async Task Sql_detail_counts_only_submitted_attempts_and_scopes_and_pages_history()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mine = new Course { Title = "Mine", InstructorId = "owner-id" };
        var other = new Course { Title = "Other", InstructorId = "another" };
        var quiz = new Quiz { Title = "Quiz", Course = mine };
        var otherQuiz = new Quiz { Title = "Private quiz", Course = other };
        var enrollment = new Enrollment { UserId = "student", Course = mine };
        db.Enrollments.Add(enrollment); db.Quizzes.AddRange(quiz, otherQuiz);
        var started = DateTime.UtcNow.AddDays(-1);
        for (var i = 0; i < 12; i++) db.QuizAttempts.Add(new QuizAttempt
        {
            Quiz = quiz, UserId = "student", StartedAt = started.AddMinutes(i),
            CompletedAt = i == 0 ? null : started.AddMinutes(i + 10), Score = i == 0 ? 999.99m : 50 + i * 5,
            Passed = i % 2 == 0
        });
        db.QuizAttempts.Add(new QuizAttempt { Quiz = otherQuiz, UserId = "student", Score = 909 });
        db.QuizAttempts.Add(new QuizAttempt { Quiz = quiz, UserId = "someone-else", Score = 908 });
        await db.SaveChangesAsync();
        var repository = new InstructorStudentRepository(db);
        var actor = new InstructorIdentity("owner-id", "owner@example.test");
        var first = await repository.DetailAsync(actor, mine.Id, enrollment.Id, 1);
        Assert.NotNull(first); Assert.Equal(12, first.TotalCount); Assert.Equal(11, first.SubmittedCount); Assert.Equal(5, first.PassedCount);
        Assert.Equal(started.AddMinutes(21), first.LastSubmittedAt); Assert.Equal(10, first.Attempts.Count);
        var last = await repository.DetailAsync(actor, mine.Id, enrollment.Id, int.MaxValue);
        Assert.NotNull(last); Assert.Equal(2, last.Page); Assert.Equal(2, last.Attempts.Count);
        var pending = Assert.Single(first.Attempts.Concat(last.Attempts).Where(a => a.CompletedAt is null));
        Assert.Null(pending.Score); Assert.Null(pending.Passed);
        Assert.Null(await repository.DetailAsync(actor, other.Id, enrollment.Id, 1));
        Assert.Equal("Tài khoản không còn tồn tại", first.Student.FullName);
        var export = await repository.ExportAsync(actor, mine.Id, null, null);
        Assert.NotNull(export); Assert.Equal(12, export.Attempts.Count);
        Assert.All(export.Attempts, a => Assert.Equal(quiz.Id, a.QuizId));
        var pendingExport = Assert.Single(export.Attempts.Where(a => a.Attempt.CompletedAt is null));
        Assert.Null(pendingExport.Attempt.Score); Assert.Null(pendingExport.Attempt.Passed);
    }
}
