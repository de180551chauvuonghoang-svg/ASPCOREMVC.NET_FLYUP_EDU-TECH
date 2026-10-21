using System.Net;
using Microsoft.AspNetCore.Identity;

namespace EduFlyUp.Tests;

public sealed class AccountSecurityTests
{
    [Fact]
    public async Task Active_admin_can_sign_in_with_a_persistent_cookie()
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("admin@example.test", "Admin");
        using var browser = app.CreateBrowser();
        var login = await TestApplication.LoginAsync(browser, "admin@example.test", remember: true);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/Admin", login.Headers.Location?.ToString());
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), c => c.Contains(".AspNetCore.Identity.Application") && c.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(HttpStatusCode.OK, (await browser.Client.GetAsync("/Admin")).StatusCode);
    }

    [Fact]
    public async Task Inactive_user_cannot_obtain_a_cookie_even_with_the_correct_password()
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("inactive@example.test", "Admin", active: false);
        using var browser = app.CreateBrowser();
        var login = await TestApplication.LoginAsync(browser, "inactive@example.test");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Null(browser.AuthCookie);
        Assert.Contains("không được phép đăng nhập", System.Net.WebUtility.HtmlDecode(await login.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Existing_cookie_is_rejected_on_the_next_request_after_deactivation()
    {
        using var app = new TestApplication();
        var id = await app.CreateUserAsync("admin@example.test", "Admin");
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "admin@example.test");
        await app.ChangeUserAsync(id, async (users, user) =>
        {
            user.IsActive = false;
            Assert.True((await users.UpdateAsync(user)).Succeeded);
        });
        var response = await browser.Client.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString());
        Assert.Contains("accountUnavailable=true", response.Headers.Location?.ToString());
        Assert.Null(browser.AuthCookie);

        await app.ChangeUserAsync(id, async (users, user) =>
        {
            user.IsActive = true;
            Assert.True((await users.UpdateAsync(user)).Succeeded);
        });
        Assert.Equal(HttpStatusCode.Redirect, (await browser.Client.GetAsync("/Admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await TestApplication.LoginAsync(browser, "admin@example.test")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.Client.GetAsync("/Admin")).StatusCode);
    }

    [Fact]
    public async Task Deleted_user_cookie_is_rejected_without_waiting_for_the_default_interval()
    {
        using var app = new TestApplication();
        var id = await app.CreateUserAsync("admin@example.test", "Admin");
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "admin@example.test");
        await app.ChangeUserAsync(id, async (users, user) => Assert.True((await users.DeleteAsync(user)).Succeeded));
        Assert.Equal(HttpStatusCode.Redirect, (await browser.Client.GetAsync("/Admin")).StatusCode);
        Assert.Null(browser.AuthCookie);
    }

    [Fact]
    public async Task Changed_security_stamp_invalidates_a_cookie_immediately()
    {
        using var app = new TestApplication();
        var id = await app.CreateUserAsync("admin@example.test", "Admin");
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "admin@example.test");
        var oldCookie = browser.AuthCookie;
        await app.ChangeUserAsync(id, async (users, user) => Assert.True((await users.UpdateSecurityStampAsync(user)).Succeeded));
        using var replay = app.CreateBrowser(oldCookie);
        Assert.Equal(HttpStatusCode.Redirect, (await replay.Client.GetAsync("/Admin")).StatusCode);
        Assert.Null(replay.AuthCookie);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Instructor")]
    [InlineData("Teacher")]
    public async Task Authenticated_non_admin_is_sent_to_a_working_access_denied_page(string role)
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("user@example.test", role);
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "user@example.test");
        var response = await browser.Client.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location?.ToString());
        Assert.Equal(HttpStatusCode.OK, (await browser.Client.GetAsync(response.Headers.Location)).StatusCode);
    }

    [Fact]
    public async Task Guest_is_challenged_and_logout_requires_an_antiforgery_token()
    {
        using var app = new TestApplication();
        using var browser = app.CreateBrowser();
        var response = await browser.Client.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString());
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Client.PostAsync("/Account/Logout", new FormUrlEncodedContent([]))).StatusCode);
    }

    [Fact]
    public async Task External_return_url_is_not_followed()
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("admin@example.test", "Admin");
        using var browser = app.CreateBrowser();
        var response = await TestApplication.LoginAsync(browser, "admin@example.test", "https://example.test/outside");
        Assert.Equal("/", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Password_lockout_remains_independent_of_business_deactivation()
    {
        using var app = new TestApplication();
        var id = await app.CreateUserAsync("admin@example.test", "Admin");
        using var browser = app.CreateBrowser();
        for (var attempt = 0; attempt < 5; attempt++)
            await TestApplication.LoginAsync(browser, "admin@example.test", password: "incorrect-password");
        await app.ChangeUserAsync(id, async (users, user) =>
        {
            Assert.True(user.IsActive);
            Assert.True(await users.IsLockedOutAsync(user));
        });
        Assert.Null(browser.AuthCookie);
    }
}
