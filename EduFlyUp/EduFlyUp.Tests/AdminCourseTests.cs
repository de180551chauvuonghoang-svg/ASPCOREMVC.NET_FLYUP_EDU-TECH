using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using EduFlyUp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlyUp.Tests;

public sealed class AdminCourseTests
{
    private static Course Course(int id = 1, bool published = false) => new()
    {
        Id = id, Title = "Course " + id, InstructorId = "owner@example.test", IsPublished = published,
        Description = "Description to keep", ThumbnailUrl = "thumbnail.png", Price = 1200,
        Level = CourseLevel.Beginner, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Lessons = [new Lesson { Id = id, Title = "Lesson " + id, Content = "Private lesson content", VideoUrl = "video.mp4", Order = 1 }],
        Enrollments = [new Enrollment { Id = id, UserId = "learner", Status = EnrollmentStatus.Active }]
    };

    private static async Task SeedAsync(TestApplication app, params Course[] courses)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Courses.AddRange(courses);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Search_filters_before_paging_has_stable_order_and_resolves_legacy_owners()
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("owner@example.test", "Instructor");
        var courses = Enumerable.Range(1, 23).Select(i => Course(i, true)).ToArray();
        foreach (var course in courses) course.Title = "Needle " + course.Id;
        var other = Course(24, true); other.Title = "Needle other owner"; other.InstructorId = "seed-owner";
        var hidden = Course(25); hidden.Title = "Needle hidden";
        var advanced = Course(26, true); advanced.Title = "Needle advanced"; advanced.Level = CourseLevel.Advanced;
        await SeedAsync(app, courses.Concat([other, hidden, advanced]).ToArray());
        using var scope = app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAdminCourseService>();
        var query = new AdminCourseQuery(" Needle ", " OWNER@EXAMPLE.TEST ", CourseLevel.Beginner, true, 2);
        var page = await service.SearchAsync(query);
        Assert.Equal(23, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(Enumerable.Range(4, 10).Reverse(), page.Items.Select(c => c.Id));
        Assert.All(page.Items, item => { Assert.Equal("Instructor", item.InstructorName); Assert.Equal("owner@example.test", item.InstructorEmail); Assert.Equal(1, item.LessonCount); });
        Assert.Contains(page.Instructors, i => i.UserName == "seed-owner" && i.DisplayName == "seed-owner");
        Assert.Equal(3, (await service.SearchAsync(query with { Page = int.MaxValue })).Page);
        Assert.Equal(1, (await service.SearchAsync(query with { Page = -1 })).Page);
        var empty = await service.SearchAsync(query with { Search = "no match" });
        Assert.Empty(empty.Items); Assert.Equal(1, empty.Page); Assert.Equal(1, empty.TotalPages);
        var fallback = (await service.SearchAsync(new AdminCourseQuery(Instructor: "seed-owner"))).Items.Single();
        Assert.Null(fallback.InstructorName); Assert.Null(fallback.InstructorEmail);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Student")]
    [InlineData("Instructor")]
    [InlineData("Teacher")]
    public async Task All_admin_course_endpoints_reject_guests_and_other_roles(string? role)
    {
        using var app = new TestApplication();
        await SeedAsync(app, Course());
        using var browser = app.CreateBrowser();
        if (role is not null)
        {
            await app.CreateUserAsync("user@example.test", role);
            await TestApplication.LoginAsync(browser, "user@example.test");
        }
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Account/Login");
        var responses = new[]
        {
            await browser.Client.GetAsync("/Admin/Courses"), await browser.Client.GetAsync("/Admin/CourseDetails/1"),
            await browser.Client.PostAsync("/Admin/EnableCourse/1", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token })),
            await browser.Client.PostAsync("/Admin/DisableCourse/1", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }))
        };
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains(role is null ? "/Account/Login" : "/Account/AccessDenied", response.Headers.Location?.ToString());
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Publication_is_explicit_idempotent_and_preserves_course_and_related_data(bool published)
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("admin@example.test", "Admin");
        await SeedAsync(app, Course(published: !published));
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "admin@example.test");
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Admin/CourseDetails/1");
        var action = published ? "EnableCourse" : "DisableCourse";
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["Title"] = "forged", ["InstructorId"] = "forged",
            ["Price"] = "0", ["Id"] = "999", ["IsPublished"] = (!published).ToString(),
            ["returnUrl"] = "/Admin/Courses?Search=Course&Page=2"
        };
        DateTime? firstUpdated = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var response = await browser.Client.PostAsync("/Admin/" + action + "/1", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(form["returnUrl"], response.Headers.Location?.ToString());
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Courses.Include(c => c.Lessons).Include(c => c.Enrollments).SingleAsync();
            Assert.Equal(published, stored.IsPublished); Assert.Equal("Course 1", stored.Title);
            Assert.Equal("owner@example.test", stored.InstructorId); Assert.Equal(1200, stored.Price);
            Assert.Equal("Description to keep", stored.Description); Assert.Equal("thumbnail.png", stored.ThumbnailUrl);
            Assert.Equal(CourseLevel.Beginner, stored.Level); Assert.Equal(Course().CreatedAt, stored.CreatedAt);
            Assert.Equal("Private lesson content", Assert.Single(stored.Lessons).Content);
            Assert.Equal("video.mp4", stored.Lessons.Single().VideoUrl);
            Assert.Equal(EnrollmentStatus.Active, Assert.Single(stored.Enrollments).Status);
            Assert.NotNull(stored.UpdatedAt);
            if (attempt == 0) firstUpdated = stored.UpdatedAt; else Assert.Equal(firstUpdated, stored.UpdatedAt);
            var access = new CourseAccessService(app.Enrollments);
            var guest = new ClaimsPrincipal(new ClaimsIdentity());
            Assert.Equal(published, await access.CanViewCourseAsync(stored, guest));
            app.Enrollments.Enrollments.Clear();
            app.Enrollments.Enrollments.Add(new Enrollment { CourseId = 1, UserId = "learner", Status = EnrollmentStatus.Active });
            var learner = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "learner")], "test"));
            Assert.True(await access.CanViewCourseAsync(stored, learner));
        }
    }

    [Fact]
    public async Task Mutation_requires_post_and_token_handles_missing_id_and_rejects_external_redirects()
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("admin@example.test", "Admin");
        await SeedAsync(app, Course());
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "admin@example.test");
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Admin/CourseDetails/1");
        foreach (var action in new[] { "EnableCourse", "DisableCourse" })
        {
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (await browser.Client.GetAsync("/Admin/" + action + "/1")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await browser.Client.PostAsync("/Admin/" + action + "/1", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await browser.Client.PostAsync("/Admin/" + action + "/999", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }))).StatusCode);
        }
        var redirected = await browser.Client.PostAsync("/Admin/EnableCourse/1", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = token, ["returnUrl"] = "https://example.com" }));
        Assert.Equal("/Admin/CourseDetails/1", redirected.Headers.Location?.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await browser.Client.GetAsync("/Admin/CourseDetails/999")).StatusCode);
    }

    [Theory]
    [InlineData("Level=999")]
    [InlineData("Level=wrong")]
    [InlineData("IsPublished=wrong")]
    [InlineData("Page=wrong")]
    public async Task Invalid_filters_are_rejected(string query)
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("admin@example.test", "Admin");
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "admin@example.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Client.GetAsync("/Admin/Courses?" + query)).StatusCode);
    }

    [Fact]
    public async Task Admin_can_read_hidden_lesson_details_with_encoded_content_and_empty_states()
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("admin@example.test", "Admin");
        var course = Course();
        course.Lessons.Single().Content = "<script>alert('x')</script>";
        course.Lessons.Single().VideoUrl = "javascript:alert('x')";
        course.Lessons.Add(new Lesson { Id = 2, Title = "First lesson", Order = 0 });
        await SeedAsync(app, course, new Course { Id = 2, Title = "Empty course", InstructorId = "seed-owner" });
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "admin@example.test");
        var response = await browser.Client.GetAsync("/Admin/CourseDetails/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("&lt;script&gt;", html); Assert.DoesNotContain("<script>alert", html);
        Assert.DoesNotContain("href=\"javascript:", html);
        Assert.True(html.IndexOf("First lesson", StringComparison.Ordinal) < html.IndexOf("Lesson 1", StringComparison.Ordinal));
        Assert.Contains("/Admin/EnableCourse/1", html);
        var empty = WebUtility.HtmlDecode(await (await browser.Client.GetAsync("/Admin/CourseDetails/2")).Content.ReadAsStringAsync());
        Assert.Contains("Chưa có mô tả.", empty); Assert.Contains("chưa có bài học", empty); Assert.Contains("seed-owner", empty);
    }

    [Fact]
    public async Task Rendered_filters_pagination_and_mutation_return_url_keep_the_current_query()
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("admin@example.test", "Admin");
        await SeedAsync(app, Enumerable.Range(1, 11).Select(i => Course(i, true)).ToArray());
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "admin@example.test");
        var url = "/Admin/Courses?Search=Course&Instructor=owner%40example.test&Level=1&IsPublished=true&Page=1";
        var response = await browser.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        var link = Regex.Matches(html, "href=\"([^\"]+)\"").Cast<Match>().Select(m => m.Groups[1].Value)
            .First(h => h.Contains("Page=2"));
        Assert.Contains("Search=Course", link); Assert.Contains("Instructor=owner@example.test", Uri.UnescapeDataString(link));
        Assert.Contains("Level=Beginner", link); Assert.Contains("IsPublished=True", link);
        Assert.Contains("name=\"returnUrl\" value=\"" + url + "\"", html);
        Assert.Contains("/Admin/DisableCourse/11", html); Assert.Contains("onsubmit=\"return confirm(", html);
        Assert.Contains("/Admin/CourseDetails/11", html);
        Assert.Equal(HttpStatusCode.OK, (await browser.Client.GetAsync(link)).StatusCode);
    }

    [Fact]
    public async Task Deactivated_admin_cannot_publish_with_an_existing_cookie()
    {
        using var app = new TestApplication();
        var id = await app.CreateUserAsync("admin@example.test", "Admin");
        await SeedAsync(app, Course());
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "admin@example.test");
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Admin/CourseDetails/1");
        await app.ChangeUserAsync(id, async (users, user) => { user.IsActive = false; Assert.True((await users.UpdateAsync(user)).Succeeded); });
        var response = await browser.Client.PostAsync("/Admin/EnableCourse/1", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString());
        using var scope = app.Services.CreateScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<AppDbContext>().Courses.SingleAsync()).IsPublished);
    }
}
