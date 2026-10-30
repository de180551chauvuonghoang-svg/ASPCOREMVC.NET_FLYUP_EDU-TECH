using System.Net;
using ClosedXML.Excel;
using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlyUp.Tests;

public sealed class InstructorStudentExportTests
{
    [Fact]
    public async Task Oversized_export_returns_actionable_error_instead_of_a_partial_file()
    {
        using var app = new TestApplication();
        var owner = await app.CreateUserAsync("owner@example.test", "Instructor");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Courses.Add(new Course { Id = 1, Title = "Large course", InstructorId = owner });
            db.Enrollments.AddRange(Enumerable.Range(1, 10001).Select(i => new Enrollment { CourseId = 1, UserId = "user" + i }));
            await db.SaveChangesAsync();
        }
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "owner@example.test");
        var response = await browser.Client.GetAsync("/InstructorStudents/Export?courseId=1");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("10.000", await response.Content.ReadAsStringAsync());
        Assert.Null(response.Content.Headers.ContentDisposition);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Admin")]
    public async Task Export_requires_instructor_role(string role)
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("reader@example.test", role);
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "reader@example.test");
        Assert.Equal(HttpStatusCode.Redirect, (await browser.Client.GetAsync("/InstructorStudents/Export?courseId=1")).StatusCode);
        using var guest = app.CreateBrowser();
        Assert.Equal(HttpStatusCode.Redirect, (await guest.Client.GetAsync("/InstructorStudents/Export?courseId=1")).StatusCode);
    }

    [Fact]
    public async Task Download_is_valid_xlsx_complete_filtered_scoped_and_contains_no_formulas_or_pending_scores()
    {
        using var app = new TestApplication();
        var owner = await app.CreateUserAsync("owner@example.test", "Instructor");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Courses.AddRange(new Course { Id = 1, Title = "Khóa học tiếng Việt", InstructorId = owner },
                new Course { Id = 2, Title = "Foreign", InstructorId = "other" });
            for (var i = 1; i <= 12; i++)
            {
                db.Users.Add(new ApplicationUser { Id = "hv" + i, FullName = i == 1 ? "=HYPERLINK(\"https://example.test\")" : "Học viên " + i, Email = $"hv{i}@example.test", PasswordHash = "SECRET_HASH" });
                db.Enrollments.Add(new Enrollment { Id = i, CourseId = 1, UserId = "hv" + i,
                    Status = i == 1 ? EnrollmentStatus.Completed : EnrollmentStatus.Active, EnrolledAt = new DateTime(2026, 10, 30, 20, 0, 0),
                    CompletedAt = i == 1 ? new DateTime(2026, 10, 31) : null });
            }
            db.Quizzes.AddRange(new Quiz { Id = 1, CourseId = 1, Title = "Đề thứ nhất" }, new Quiz { Id = 2, CourseId = 2, Title = "PRIVATE_QUIZ" });
            db.QuizAttempts.AddRange(new QuizAttempt { Id = 1, QuizId = 1, UserId = "hv1", Score = 85.5m, Passed = true, CompletedAt = new DateTime(2026, 10, 31) },
                new QuizAttempt { Id = 2, QuizId = 1, UserId = "hv1", Score = 999.99m, Passed = true },
                new QuizAttempt { Id = 3, QuizId = 1, UserId = "hv1", Score = 45m, CompletedAt = new DateTime(2026, 10, 31) },
                new QuizAttempt { Id = 4, QuizId = 2, UserId = "hv1", Score = 888m, CompletedAt = new DateTime(2026, 10, 31) },
                new QuizAttempt { Id = 5, QuizId = 1, UserId = "unregistered", Score = 777m, CompletedAt = new DateTime(2026, 10, 31) });
            await db.SaveChangesAsync();
        }
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "owner@example.test");
        var response = await browser.Client.GetAsync("/InstructorStudents/Export?courseId=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("HocVien-KhoaHoc-1.xlsx", response.Content.Headers.ContentDisposition!.ToString());
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        using var workbook = new XLWorkbook(stream);
        Assert.Equal(5, workbook.Worksheets.Count);
        var students = workbook.Worksheet("Hoc vien");
        Assert.Equal(13, students.LastRowUsed()!.RowNumber());
        var formulaName = Assert.Single(students.RowsUsed().Skip(1).Where(r => r.Cell(2).GetString() == "hv1"));
        Assert.StartsWith("=HYPERLINK", formulaName.Cell(3).GetString()); Assert.False(formulaName.Cell(3).HasFormula);
        Assert.Equal(new DateTime(2026, 10, 31, 3, 0, 0), formulaName.Cell(5).GetDateTime());
        Assert.Equal("Chưa có dữ liệu", formulaName.Cell(8).GetString());
        Assert.Equal(2, workbook.Worksheet("Da hoan thanh").LastRowUsed()!.RowNumber());
        var attempts = workbook.Worksheet("Lich su diem");
        Assert.Equal(4, attempts.LastRowUsed()!.RowNumber());
        var pending = Assert.Single(attempts.RowsUsed().Skip(1).Where(r => r.Cell(8).GetValue<int>() == 2));
        Assert.True(pending.Cell(11).IsEmpty()); Assert.Equal("Chưa nộp", pending.Cell(12).GetString());
        var best = workbook.Worksheet("Diem cao nhat");
        Assert.Equal(2, best.LastRowUsed()!.RowNumber()); Assert.Equal(85.5m, best.Cell(2, 7).GetValue<decimal>()); Assert.Equal(2, best.Cell(2, 8).GetValue<int>());
        Assert.All(workbook.Worksheets.SelectMany(s => s.CellsUsed()), c =>
        {
            Assert.False(c.HasFormula); Assert.DoesNotContain("SECRET_HASH", c.GetString()); Assert.DoesNotContain("PRIVATE_QUIZ", c.GetString());
        });

        using var filteredStream = new MemoryStream(await browser.Client.GetByteArrayAsync("/InstructorStudents/Export?courseId=1&status=Completed&search=hv1%40"));
        using var filtered = new XLWorkbook(filteredStream);
        Assert.Equal(2, filtered.Worksheet("Hoc vien").LastRowUsed()!.RowNumber());
        Assert.Equal("hv1", filtered.Worksheet("Hoc vien").Cell(2, 2).GetString());
        using var emptyStream = new MemoryStream(await browser.Client.GetByteArrayAsync("/InstructorStudents/Export?courseId=1&search=nomatch"));
        using var empty = new XLWorkbook(emptyStream);
        Assert.Equal(1, empty.Worksheet("Hoc vien").LastRowUsed()!.RowNumber());
        foreach (var query in new[] { "courseId=2", "courseId=999" })
            Assert.Equal(HttpStatusCode.NotFound, (await browser.Client.GetAsync("/InstructorStudents/Export?" + query)).StatusCode);
        foreach (var query in new[] { "courseId=wrong", "courseId=0", "courseId=1&status=999", "courseId=1&search=" + new string('a', 101) })
            Assert.Equal(HttpStatusCode.BadRequest, (await browser.Client.GetAsync("/InstructorStudents/Export?" + query)).StatusCode);
    }
}
