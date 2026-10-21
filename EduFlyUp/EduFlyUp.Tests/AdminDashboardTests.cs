using System.Net;
using System.Text.RegularExpressions;
using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using EduFlyUp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlyUp.Tests;

public sealed class AdminDashboardTests
{
    [Fact]
    public async Task Empty_database_returns_zero_in_every_metric()
    {
        using var app = new TestApplication();
        Assert.Equal(new AdminDashboardStats(0, 0, 0, 0, 0, 0, 0), await StatsAsync(app));
    }

    [Fact]
    public async Task Dashboard_counts_users_once_ignores_login_lockout_and_links_to_matching_filters()
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("admin@example.test", "Admin");
        await app.CreateUserAsync("inactive@example.test", "Instructor", false);
        var student = await app.CreateUserAsync("student@example.test", "Student");
        await app.CreateUserAsync("teacher@example.test", "Teacher");
        await app.ChangeUserAsync(student, async (users, user) =>
        {
            Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);
            Assert.True((await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddHours(1))).Succeeded);
        });
        await SeedCoursesAsync(app);
        // The dashboard must query the database, not the legacy entity-list service.
        app.Courses.Courses.Add(new Course { Id = 999, Title = "Legacy fake course" });
        var expected = new AdminDashboardStats(4, 3, 1, 3, 2, 1, 5);
        Assert.Equal(expected, await StatsAsync(app));
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "admin@example.test");
        var html = await AssertDashboardAsync(browser, expected);
        var links = new[]
        {
            ("total-users", "/Admin/Users"), ("active-users", "/Admin/Users?IsActive=True"),
            ("inactive-users", "/Admin/Users?IsActive=False"), ("total-courses", "/Admin/Courses"),
            ("published-courses", "/Admin/Courses?IsPublished=True"), ("hidden-courses", "/Admin/Courses?IsPublished=False")
        };
        foreach (var (key, url) in links)
        {
            var card = Regex.Matches(html, "<a\\b[^>]*>.*?</a>", RegexOptions.Singleline)
                .Cast<Match>().Single(match => match.Value.Contains("data-testid=\"" + key + "\""));
            Assert.Contains("href=\"" + url + "\"", card.Value);
            Assert.Equal(HttpStatusCode.OK, (await browser.Client.GetAsync(url)).StatusCode);
        }
        Assert.DoesNotContain("SQL Server Live", html);
        Assert.DoesNotContain("Legacy fake course", html);
        Assert.DoesNotContain("<table", html);
        var alias = await browser.Client.GetAsync("/Admin/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, alias.StatusCode);
        Assert.Equal("/Admin", alias.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Dashboard_refresh_reflects_account_and_publication_actions_without_changing_totals()
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("admin@example.test", "Admin");
        var student = await app.CreateUserAsync("student@example.test", "Student");
        await SeedCoursesAsync(app);
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "admin@example.test");
        await AssertDashboardAsync(browser, new(2, 2, 0, 3, 2, 1, 5));
        await PostAsync(browser, "DeactivateUser/" + student, "/Admin/UserDetails/" + student);
        await AssertDashboardAsync(browser, new(2, 1, 1, 3, 2, 1, 5));
        await PostAsync(browser, "DisableCourse/1", "/Admin/CourseDetails/1");
        await AssertDashboardAsync(browser, new(2, 1, 1, 3, 1, 2, 5));
        await PostAsync(browser, "ActivateUser/" + student, "/Admin/UserDetails/" + student);
        await AssertDashboardAsync(browser, new(2, 2, 0, 3, 1, 2, 5));
        await PostAsync(browser, "EnableCourse/1", "/Admin/CourseDetails/1");
        await AssertDashboardAsync(browser, new(2, 2, 0, 3, 2, 1, 5));
    }

    [Fact]
    public void Production_queries_translate_to_sql_aggregates_without_loading_entities_or_roles()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(local);Database=TranslationOnly;Integrated Security=True;TrustServerCertificate=True").Options);
        var userSql = AdminDashboardRepository.UsersQuery(db).ToQueryString();
        var courseSql = AdminDashboardRepository.CoursesQuery(db).ToQueryString();
        Assert.Contains("COUNT(", userSql); Assert.Contains("IsActive", userSql);
        Assert.Contains("COUNT(", courseSql); Assert.Contains("IsPublished", courseSql);
        Assert.DoesNotContain("PasswordHash", userSql); Assert.DoesNotContain("JOIN", userSql);
        Assert.DoesNotContain("[Title]", courseSql); Assert.DoesNotContain("JOIN", courseSql);
    }

    internal static async Task SeedCoursesAsync(TestApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Courses.AddRange(Enumerable.Range(1, 3).Select(i => new Course
        {
            Id = i, Title = "Course " + i, InstructorId = "owner@example.test", IsPublished = i != 3,
            Lessons = Enumerable.Range(1, i == 1 ? 3 : 1).Select(j => new Lesson { Title = "Lesson " + j }).ToList()
        }));
        await db.SaveChangesAsync();
    }

    private static async Task<AdminDashboardStats> StatsAsync(TestApplication app)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAdminDashboardService>().GetStatsAsync();
    }

    private static async Task<string> AssertDashboardAsync(TestBrowser browser, AdminDashboardStats expected)
    {
        var response = await browser.Client.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        var values = new[]
        {
            ("total-users", expected.TotalUsers), ("active-users", expected.ActiveUsers), ("inactive-users", expected.InactiveUsers),
            ("total-courses", expected.TotalCourses), ("published-courses", expected.PublishedCourses),
            ("hidden-courses", expected.HiddenCourses), ("total-lessons", expected.TotalLessons)
        };
        foreach (var (key, count) in values)
            Assert.Contains("data-testid=\"" + key + "\">" + count.ToString("N0") + "</strong>", html);
        return html;
    }

    private static async Task PostAsync(TestBrowser browser, string action, string formUrl)
    {
        var token = await TestApplication.AntiForgeryTokenAsync(browser, formUrl);
        var response = await browser.Client.PostAsync("/Admin/" + action, new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
