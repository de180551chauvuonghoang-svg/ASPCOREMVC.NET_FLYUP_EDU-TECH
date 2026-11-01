using System.Net;
using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Tests;

public sealed class CouponHttpTests
{
    private static Dictionary<string, string> Form(string token) => new()
    {
        ["__RequestVerificationToken"] = token, ["Code"] = " flyup-50 ", ["DiscountPercent"] = "50", ["MaxUsage"] = "100",
        ["StartDate"] = "2026-01-01T08:00", ["EndDate"] = "2099-01-01T08:00", ["CourseId"] = "1"
    };
    private static void Seed(TestApplication app)
    {
        app.Coupons.Courses.Add(new Course { Id = 1, Title = "My course", InstructorId = "owner@example.test" });
        app.Coupons.Courses.Add(new Course { Id = 2, Title = "FOREIGN_COURSE", InstructorId = "other@example.test" });
    }

    [Theory]
    [InlineData("Student", HttpStatusCode.Redirect)]
    [InlineData("Teacher", HttpStatusCode.Redirect)]
    [InlineData("Admin", HttpStatusCode.Redirect)]
    [InlineData("Instructor", HttpStatusCode.OK)]
    public async Task Management_is_only_available_to_instructors(string role, HttpStatusCode expected)
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("owner@example.test", role);
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");
        Assert.Equal(expected, (await browser.Client.GetAsync("/Coupons")).StatusCode);
        using var guest = app.CreateBrowser();
        Assert.Equal(HttpStatusCode.Redirect, (await guest.Client.GetAsync("/Coupons")).StatusCode);
    }

    [Fact]
    public async Task Create_ignores_forged_owner_usage_and_state_and_converts_vietnam_time_to_utc()
    {
        using var app = new TestApplication(); Seed(app);
        var id = await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "owner@example.test");
        var html = await browser.Client.GetStringAsync("/Coupons/Create");
        Assert.DoesNotContain("FOREIGN_COURSE", html);
        var form = Form(await TestApplication.AntiForgeryTokenAsync(browser, "/Coupons/Create"));
        form["InstructorId"] = "hacker"; form["UsedCount"] = "999"; form["IsActive"] = "false"; form["Id"] = "999";
        var response = await browser.Client.PostAsync("/Coupons/Create", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Coupons", response.Headers.Location?.ToString());
        var created = Assert.Single(app.Coupons.Coupons);
        Assert.Equal(id, created.InstructorId); Assert.Equal(0, created.UsedCount); Assert.True(created.IsActive);
        Assert.Equal("FLYUP-50", created.Code);
        Assert.Equal(new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc), created.StartDate);
        Assert.Equal(1, created.CourseId);
    }

    [Theory]
    [InlineData("DiscountPercent", "91")]
    [InlineData("MaxUsage", "0")]
    [InlineData("EndDate", "2020-01-01T00:00")]
    [InlineData("StartDate", "0001-01-01T00:00")]
    [InlineData("CourseId", "2")]
    public async Task Invalid_input_or_foreign_course_returns_form_and_does_not_save(string field, string value)
    {
        using var app = new TestApplication(); Seed(app);
        await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "owner@example.test");
        var form = Form(await TestApplication.AntiForgeryTokenAsync(browser, "/Coupons/Create"));
        form[field] = value;
        var response = await browser.Client.PostAsync("/Coupons/Create", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("My course", await response.Content.ReadAsStringAsync());
        Assert.Empty(app.Coupons.Coupons);
    }

    [Fact]
    public async Task Other_instructor_cannot_read_edit_or_disable_coupon_and_missing_token_is_rejected()
    {
        using var app = new TestApplication(); Seed(app);
        app.Coupons.Coupons.Add(new Coupon { Id = 1, InstructorId = "other@example.test", Code = "FOREIGNCODE" });
        await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "owner@example.test");
        Assert.DoesNotContain("FOREIGNCODE", await browser.Client.GetStringAsync("/Coupons"));
        Assert.Equal(HttpStatusCode.NotFound, (await browser.Client.GetAsync("/Coupons/Edit/1")).StatusCode);
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Coupons/Create");
        var form = Form(token); form["Id"] = "1";
        Assert.Equal(HttpStatusCode.NotFound, (await browser.Client.PostAsync("/Coupons/Edit/1", new FormUrlEncodedContent(form))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.Client.PostAsync("/Coupons/Deactivate/1", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = token, ["version"] = "123" }))).StatusCode);
        form.Remove("__RequestVerificationToken");
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Client.PostAsync("/Coupons/Create", new FormUrlEncodedContent(form))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Client.PostAsync("/Coupons/Deactivate/1", new FormUrlEncodedContent(form))).StatusCode);
        Assert.True(app.Coupons.Coupons.Single().IsActive);
    }

    [Fact]
    public async Task Owner_can_edit_and_disable_enable_but_stale_form_returns_conflict()
    {
        using var app = new TestApplication(); Seed(app);
        app.Coupons.Coupons.Add(new Coupon { Id = 1, InstructorId = "owner@example.test", Code = "ORIGINAL", EndDate = DateTime.UtcNow.AddDays(7) });
        await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "owner@example.test");
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Coupons/Edit/1");
        var stored = app.Coupons.Coupons.Single();
        var form = Form(token); form["Id"] = "1"; form["Version"] = CouponPresentation.Version(stored);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.Client.PostAsync("/Coupons/Edit/1", new FormUrlEncodedContent(form))).StatusCode);
        Assert.Equal("FLYUP-50", stored.Code);
        Assert.Equal(HttpStatusCode.Conflict, (await browser.Client.PostAsync("/Coupons/Edit/1", new FormUrlEncodedContent(form))).StatusCode);
        foreach (var action in new[] { "Deactivate", "Activate" })
        {
            var stateForm = new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["version"] = CouponPresentation.Version(stored) };
            Assert.Equal(HttpStatusCode.Redirect, (await browser.Client.PostAsync("/Coupons/" + action + "/1", new FormUrlEncodedContent(stateForm))).StatusCode);
            Assert.Equal(action == "Activate", stored.IsActive);
        }
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await browser.Client.GetAsync("/Coupons/Deactivate/1")).StatusCode);
    }

    [Fact]
    public async Task Duplicate_error_keeps_values_and_renders_untrusted_text_safely()
    {
        using var app = new TestApplication(); Seed(app);
        app.Coupons.NextSaveResult = CouponChange.DuplicateCode;
        await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "owner@example.test");
        var form = Form(await TestApplication.AntiForgeryTokenAsync(browser, "/Coupons/Create"));
        var response = await browser.Client.PostAsync("/Coupons/Create", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("flyup-50", html); Assert.Contains("My course", html);
        var searched = await browser.Client.GetStringAsync("/Coupons?search=" + Uri.EscapeDataString("<script>alert(1)</script>"));
        Assert.DoesNotContain("<script>alert(1)</script>", searched);
    }
}
