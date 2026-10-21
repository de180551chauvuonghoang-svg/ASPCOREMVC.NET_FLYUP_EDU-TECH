using System.Net;
using System.Text.RegularExpressions;
using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Tests;

public sealed class CourseViewTests
{
    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Instructor", false)]
    public async Task Only_admin_has_publication_inputs_and_edit_delete_forms_are_independent(string role, bool canPublish)
    {
        using var app = new TestApplication();
        app.Courses.Courses.Add(new Course { Id = 1, InstructorId = "owner@example.test", Title = "Owned course" });
        await app.CreateUserAsync("owner@example.test", role);
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");
        foreach (var path in new[] { "/Courses/Create", "/Courses/Edit/1" })
        {
            var response = await browser.Client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Equal(canPublish, html.Contains("name=\"IsPublished\""));
            var depth = 0;
            foreach (Match tag in Regex.Matches(html, "</?form\\b[^>]*>", RegexOptions.IgnoreCase))
            {
                if (tag.Value.StartsWith("</", StringComparison.Ordinal)) { Assert.Equal(1, depth); depth--; }
                else { Assert.Equal(0, depth); depth++; }
            }
            Assert.Equal(0, depth);
            if (path.Contains("Edit"))
            {
                Assert.Contains("form=\"delete-course-form\"", html);
                Assert.Matches("<form[^>]*id=\"delete-course-form\"[^>]*action=\"/Courses/Delete/1\"", html);
            }
        }
    }

    [Theory]
    [InlineData("Admin", "admin@example.test", true)]
    [InlineData("Instructor", "owner@example.test", true)]
    [InlineData("Instructor", "other@example.test", false)]
    [InlineData("Student", "student@example.test", false)]
    [InlineData("Teacher", "teacher@example.test", false)]
    public async Task Detail_edit_link_matches_server_management_rights(string role, string email, bool canManage)
    {
        using var app = new TestApplication();
        app.Courses.Courses.Add(new Course { Id = 1, InstructorId = "owner@example.test", IsPublished = true, Title = "Published course" });
        await app.CreateUserAsync(email, role);
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, email);
        var response = await browser.Client.GetAsync("/Courses/Details/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(canManage, (await response.Content.ReadAsStringAsync()).Contains("/Courses/Edit/1"));
    }

    [Fact]
    public async Task Teacher_does_not_see_management_links_in_the_public_catalog_or_menu()
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("teacher@example.test", "Teacher");
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "teacher@example.test");
        var html = await (await browser.Client.GetAsync("/Courses")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("/Courses/Create", html);
        Assert.DoesNotContain("/Instructor/Dashboard", html);
    }

    [Fact]
    public async Task Dashboard_delete_confirmation_does_not_embed_a_course_title_in_javascript()
    {
        using var app = new TestApplication();
        app.Courses.Courses.Add(new Course { Id = 1, InstructorId = "owner@example.test", Title = "Course 'one' with \"quotes\"" });
        await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");
        var html = await (await browser.Client.GetAsync("/Instructor/Dashboard")).Content.ReadAsStringAsync();
        var confirmation = Regex.Matches(html, "onsubmit=\"([^\"]+)\"")
            .Cast<Match>().Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).Single();
        Assert.Equal("return confirm('Bạn có chắc muốn xóa khóa học này không?');", confirmation);
    }
}
