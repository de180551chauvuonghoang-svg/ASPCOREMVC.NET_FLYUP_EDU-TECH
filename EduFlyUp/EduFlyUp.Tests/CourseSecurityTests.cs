using System.Net;
using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Tests;

public sealed class CourseSecurityTests
{
    private static Course Course(int id = 1, string owner = "owner@example.test", bool published = false)
        => new() { Id = id, Title = "Private course " + id, Description = "Course description", InstructorId = owner, IsPublished = published, Level = CourseLevel.Beginner };

    private static Dictionary<string, string> Form(string token, int id = 1, bool published = true)
        => new()
        {
            ["__RequestVerificationToken"] = token, ["Id"] = id.ToString(),
            ["Title"] = "Updated course title", ["Description"] = "Updated course description",
            ["Price"] = "1000", ["Level"] = "1", ["IsPublished"] = published.ToString(),
            ["InstructorId"] = "forged@example.test"
        };

    [Fact]
    public async Task Instructor_creation_ignores_forged_publication_and_owner_fields()
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Courses/Create");
        var response = await browser.Client.PostAsync("/Courses/Create", new FormUrlEncodedContent(Form(token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Instructor/Dashboard", response.Headers.Location?.ToString());
        var created = Assert.Single(app.Courses.Courses);
        Assert.False(created.IsPublished);
        Assert.Equal("owner@example.test", created.InstructorId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Instructor_content_edit_cannot_change_publication(bool published)
    {
        using var app = new TestApplication();
        app.Courses.Courses.Add(Course(published: published));
        await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Courses/Edit/1");
        var response = await browser.Client.PostAsync("/Courses/Edit/1", new FormUrlEncodedContent(Form(token, published: !published)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(published, app.Courses.Courses[0].IsPublished);
        Assert.Equal("Updated course title", app.Courses.Courses[0].Title);
        Assert.Equal("owner@example.test", app.Courses.Courses[0].InstructorId);
        Assert.Equal(1, app.Courses.ContentUpdates);
        Assert.Equal(0, app.Courses.PublicationUpdates);
    }

    [Fact]
    public async Task Content_edit_uses_a_write_path_that_cannot_restore_an_admins_concurrent_hide()
    {
        using var app = new TestApplication();
        app.Courses.Courses.Add(Course(published: true));
        app.Courses.BeforeContentUpdate = () => app.Courses.Courses[0].IsPublished = false;
        await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Courses/Edit/1");
        var response = await browser.Client.PostAsync("/Courses/Edit/1", new FormUrlEncodedContent(Form(token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(app.Courses.Courses[0].IsPublished);
        Assert.Equal(0, app.Courses.PublicationUpdates);
    }

    [Fact]
    public async Task Instructor_cannot_edit_or_delete_another_instructors_course_via_direct_requests()
    {
        using var app = new TestApplication();
        app.Courses.Courses.Add(Course());
        app.Courses.Courses.Add(Course(2, "other@example.test"));
        await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Courses/Edit/1");
        var get = await browser.Client.GetAsync("/Courses/Edit/2");
        var edit = await browser.Client.PostAsync("/Courses/Edit/2", new FormUrlEncodedContent(Form(token, 2)));
        var delete = await browser.Client.PostAsync("/Courses/Delete/2", new FormUrlEncodedContent(Form(token, 2)));
        foreach (var response in new[] { get, edit, delete })
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/AccessDenied", response.Headers.Location?.ToString());
        }
        Assert.Equal("Private course 2", app.Courses.Courses.Single(c => c.Id == 2).Title);
        Assert.Equal(2, app.Courses.Courses.Count);
    }

    [Fact]
    public async Task Admin_can_edit_another_owners_course_and_control_publication()
    {
        using var app = new TestApplication();
        app.Courses.Courses.Add(Course());
        await app.CreateUserAsync("admin@example.test", "Admin");
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "admin@example.test");
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Courses/Edit/1");
        Assert.Equal(HttpStatusCode.Redirect, (await browser.Client.PostAsync("/Courses/Edit/1", new FormUrlEncodedContent(Form(token)))).StatusCode);
        Assert.True(app.Courses.Courses[0].IsPublished);
        Assert.Equal(1, app.Courses.PublicationUpdates);
        Assert.Equal("owner@example.test", app.Courses.Courses[0].InstructorId);
    }

    [Fact]
    public async Task Instructor_dashboard_has_no_seed_or_all_courses_fallback()
    {
        using var app = new TestApplication();
        app.Courses.Courses.Add(Course());
        app.Courses.Courses.Add(Course(2, "instructor@eduflyup.vn"));
        app.Courses.Courses.Add(Course(3, "seed-instructor-1"));
        await app.CreateUserAsync("new@example.test", "Instructor");
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "new@example.test");
        var response = await browser.Client.GetAsync("/Instructor/Dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        foreach (var course in app.Courses.Courses) Assert.DoesNotContain(course.Title, html);
    }

    [Theory]
    [InlineData(EnrollmentStatus.Active, HttpStatusCode.OK)]
    [InlineData(EnrollmentStatus.Completed, HttpStatusCode.OK)]
    [InlineData(EnrollmentStatus.Cancelled, HttpStatusCode.NotFound)]
    public async Task Hidden_details_use_learning_access_not_just_enrollment_existence(EnrollmentStatus status, HttpStatusCode expected)
    {
        using var app = new TestApplication();
        app.Courses.Courses.Add(Course());
        var id = await app.CreateUserAsync("student@example.test", "Student");
        app.Enrollments.Enrollments.Add(new Enrollment { CourseId = 1, UserId = id, Status = status });
        using var browser = app.CreateBrowser();
        Assert.Equal(HttpStatusCode.NotFound, (await browser.Client.GetAsync("/Courses/Details/1")).StatusCode);
        await TestApplication.LoginAsync(browser, "student@example.test");
        Assert.Equal(expected, (await browser.Client.GetAsync("/Courses/Details/1")).StatusCode);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Teacher")]
    public async Task Non_instructors_cannot_manage_even_a_course_matching_their_name(string role)
    {
        using var app = new TestApplication();
        app.Courses.Courses.Add(Course(published: true));
        await app.CreateUserAsync("owner@example.test", role);
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");
        var response = await browser.Client.GetAsync("/Courses/Edit/1");
        Assert.Contains("/Account/AccessDenied", response.Headers.Location?.ToString());
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Account/Login");
        foreach (var path in new[] { "/Courses/Create", "/Courses/Edit/1", "/Courses/Delete/1" })
        {
            var mutation = await browser.Client.PostAsync(path, new FormUrlEncodedContent(Form(token)));
            Assert.Equal(HttpStatusCode.Redirect, mutation.StatusCode);
            Assert.Contains("/Account/AccessDenied", mutation.Headers.Location?.ToString());
        }
        Assert.Equal("Private course 1", app.Courses.Courses[0].Title);
    }

    [Fact]
    public async Task Course_mutations_require_tokens_and_matching_ids()
    {
        using var app = new TestApplication();
        app.Courses.Courses.Add(Course());
        await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");
        foreach (var path in new[] { "/Courses/Create", "/Courses/Edit/1", "/Courses/Delete/1" })
            Assert.Equal(HttpStatusCode.BadRequest, (await browser.Client.PostAsync(path, new FormUrlEncodedContent(Form("")))).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await browser.Client.GetAsync("/Courses/Delete/1")).StatusCode);
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Courses/Edit/1");
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Client.PostAsync("/Courses/Edit/1", new FormUrlEncodedContent(Form(token, 2)))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.Client.GetAsync("/Courses/Edit/999")).StatusCode);
        Assert.Single(app.Courses.Courses);
    }
}
