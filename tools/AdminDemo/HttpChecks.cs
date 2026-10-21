using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace AdminDemo;

internal sealed class HttpChecks(DemoSession session, DemoReport report)
{
    private DemoBrowser admin = null!;
    private DemoBrowser owner = null!;
    private DemoAccount Account(string key) => session.Accounts[key];
    private int CourseA => session.Courses["a"];
    private string CourseUrl => "/Courses/Details/" + CourseA;
    private string CourseForm => "/Admin/CourseDetails/" + CourseA;

    public async Task RunAsync()
    {
        using var a = await SignInAsync("admin-a"); admin = a;
        using var o = await SignInAsync("instructor-a"); owner = o;
        await CaseAsync("Production startup, migrations and shared Identity/DAO database", async () =>
        {
            await using var db = session.OpenDb();
            Require(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Pending migrations in demo DB.");
            using var guest = new DemoBrowser(session.BaseUrl);
            Require((await guest.HtmlAsync(CourseUrl)).Contains("DEMO course a"), "Public DAO did not read seeded SQL data.");
            await DashboardAsync();
        });
        await CaseAsync("Admin role matrix and inactive login", RolesAsync);
        await CaseAsync("SQL filters, paging, details and empty states", ListsAsync);
        await CaseAsync("Student/Instructor sessions revoked, activation and old-cookie replay", CookiesAsync);
        await CaseAsync("Instructor ownership and forged publication fields", OwnershipAsync);
        await CaseAsync("Publication, enrollment access, preserved data and dashboard", PublicationAsync);
        await CaseAsync("POST/token/ID guards and idempotent account changes", MutationsAsync);
        await CaseAsync("Concurrent Admin HTTP requests preserve an active Admin", ConcurrentAdminsAsync);
    }

    private async Task CaseAsync(string name, Func<Task> action)
    {
        var watch = Stopwatch.StartNew();
        try { await action(); report.Checks.Add(new(name, true, watch.Elapsed.TotalSeconds)); Console.WriteLine("PASS " + name); }
        catch { report.Checks.Add(new(name, false, watch.Elapsed.TotalSeconds)); throw; }
    }

    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Status(HttpResponseMessage response, HttpStatusCode status)
        => Require(response.StatusCode == status, $"{response.RequestMessage?.RequestUri?.AbsolutePath}: expected {status}, got {response.StatusCode}.");

    internal static void Redirect(HttpResponseMessage response, string path)
    {
        Status(response, HttpStatusCode.Redirect);
        var request = response.RequestMessage?.RequestUri;
        var location = response.Headers.Location;
        Require(request is { IsAbsoluteUri: true } && location is not null, "Redirect has no request URI or Location.");
        var destination = new Uri(request!, location!);
        Require(destination.GetLeftPart(UriPartial.Authority).Equals(request!.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase)
            && destination.AbsolutePath.Equals(path, StringComparison.Ordinal),
            $"{request.AbsolutePath}: expected same-origin redirect to {path}, got {destination.GetLeftPart(UriPartial.Authority)}{destination.AbsolutePath}.");
    }

    private async Task<DemoBrowser> SignInAsync(string key)
    {
        var browser = new DemoBrowser(session.BaseUrl);
        try
        {
            using var response = await browser.LoginAsync(Account(key), session.Password);
            Status(response, HttpStatusCode.Redirect); Require(browser.AuthCookie is not null, "No cookie for " + key);
            return browser;
        }
        catch { browser.Dispose(); throw; }
    }

    private async Task RolesAsync()
    {
        using var guest = new DemoBrowser(session.BaseUrl);
        foreach (var path in new[] { "/Admin", "/Admin/Users", "/Admin/Courses", CourseForm, "/Admin/UserDetails/" + Account("admin-a").Id })
        {
            using var response = await guest.Client.GetAsync(path); Redirect(response, "/Account/Login");
        }
        foreach (var key in new[] { "student-none", "instructor-a", "teacher" })
        {
            using var browser = await SignInAsync(key);
            foreach (var path in new[] { "/Admin", "/Admin/Users", "/Admin/Courses" })
            { using var response = await browser.Client.GetAsync(path); Redirect(response, "/Account/AccessDenied"); }
            await browser.HtmlAsync("/Account/AccessDenied");
        }
        using var blocked = new DemoBrowser(session.BaseUrl);
        using var denied = await blocked.LoginAsync(Account("student-blocked"), session.Password);
        Status(denied, HttpStatusCode.OK); Require(blocked.AuthCookie is null, "Inactive user received an auth cookie.");
        await admin.HtmlAsync("/Admin");
    }

    private async Task ListsAsync()
    {
        var users = await admin.HtmlAsync("/Admin/Users?Search=DEMO&Role=Student&IsActive=True&Page=2");
        Require(users.Contains("Trang 2 / 2") && users.Contains("student-") && !users.Contains("student-blocked@"), "User paging/filter mismatch.");
        Require(users.Contains("Role=Student") && users.Contains("IsActive=True") && users.Contains("Page=1"), "User paging lost filters.");
        var courses = await admin.HtmlAsync("/Admin/Courses?Search=DEMO&Page=2");
        Require(courses.Contains("Trang 2 / 2") && courses.Contains("Search=DEMO"), "Course paging/filter mismatch.");
        await using var db = session.OpenDb();
        var matching = await db.Courses.Where(c => c.Title.Contains("DEMO") && c.InstructorId == Account("instructor-a").Email
            && c.Level == CourseLevel.Beginner && !c.IsPublished).CountAsync();
        var filtered = await admin.HtmlAsync("/Admin/Courses?Search=DEMO&Instructor=" + Uri.EscapeDataString(Account("instructor-a").Email) + "&Level=1&IsPublished=False");
        Require(filtered.Contains(matching + " khóa học"), "Combined SQL course filters mismatch.");
        Require((await admin.HtmlAsync("/Admin/Users?Search=no-such-demo-profile")).Contains("Không tìm thấy tài khoản"), "Missing user empty state.");
        Require((await admin.HtmlAsync("/Admin/Courses?Search=no-such-demo-course")).Contains("Không tìm thấy khóa học"), "Missing course empty state.");
        var details = await admin.HtmlAsync("/Admin/UserDetails/" + Account("student-active").Id);
        Require(details.Contains("DEMO course a") && details.Contains(Account("student-active").Email), "Enrollment/user details mismatch.");
        var ownerDetails = await admin.HtmlAsync("/Admin/UserDetails/" + Account("instructor-a").Id);
        Require(ownerDetails.Contains("DEMO course a"), "Owner details did not resolve username to courses.");
        Require((await admin.HtmlAsync("/Admin/CourseDetails/" + session.Courses["empty"])).Contains("chưa có bài học"), "Missing lesson empty state.");
        foreach (var url in new[] { "/Admin/Users?IsActive=wrong", "/Admin/Courses?Level=999" })
        { using var invalid = await admin.Client.GetAsync(url); Status(invalid, HttpStatusCode.BadRequest); }
    }

    private async Task CookiesAsync()
    {
        foreach (var key in new[] { "student-active", "instructor-a" })
        {
            var target = Account(key);
            using var browser = await SignInAsync(key);
            var oldCookie = browser.AuthCookie;
            var before = await UserFingerprintAsync(target.Id);
            var form = "/Admin/UserDetails/" + target.Id;
            using (var changed = await admin.PostAsync("/Admin/DeactivateUser/" + target.Id, form)) Status(changed, HttpStatusCode.Redirect);
            await using (var db = session.OpenDb()) Require(!await db.Users.Where(u => u.Id == target.Id).Select(u => u.IsActive).SingleAsync(), "Deactivation did not persist.");
            await DashboardAsync();
            var protectedUrl = key == "instructor-a" ? "/Courses/Edit/" + CourseA : "/Admin";
            using (var invalid = await browser.Client.GetAsync(protectedUrl)) Redirect(invalid, "/Account/Login");
            Require(browser.AuthCookie is null, "Revoked cookie was not removed.");
            using (var login = new DemoBrowser(session.BaseUrl))
            { using var denied = await login.LoginAsync(target, session.Password); Status(denied, HttpStatusCode.OK); Require(login.AuthCookie is null, "Locked target signed in."); }
            using (var changed = await admin.PostAsync("/Admin/ActivateUser/" + target.Id, form)) Status(changed, HttpStatusCode.Redirect);
            using (var replay = new DemoBrowser(session.BaseUrl, oldCookie))
            { using var invalid = await replay.Client.GetAsync(protectedUrl); Redirect(invalid, "/Account/Login"); Require(replay.AuthCookie is null, "Old cookie revived after activation."); }
            using var fresh = await SignInAsync(key);
            using (var allowed = await fresh.Client.GetAsync(protectedUrl))
            { if (key == "instructor-a") Status(allowed, HttpStatusCode.OK); else Redirect(allowed, "/Account/AccessDenied"); }
            Require(before == await UserFingerprintAsync(target.Id), "Account roles/password/lockout/profile changed during status cycle.");
            await DashboardAsync();
        }
        // Instructor cycle revoked the browser held by the runner too.
        owner.Dispose(); owner = await SignInAsync("instructor-a");
    }

    private async Task OwnershipAsync()
    {
        var otherId = session.Courses["b"];
        using (var denied = await owner.Client.GetAsync("/Courses/Edit/" + otherId)) Redirect(denied, "/Account/AccessDenied");
        var fields = await EditFieldsAsync(otherId);
        using (var denied = await owner.PostAsync("/Courses/Edit/" + otherId, "/Courses/Edit/" + CourseA, fields)) Redirect(denied, "/Account/AccessDenied");
        using (var denied = await owner.PostAsync("/Courses/Delete/" + otherId, "/Courses/Edit/" + CourseA)) Redirect(denied, "/Account/AccessDenied");
        using var created = await owner.PostAsync("/Courses/Create", "/Courses/Create", new()
        {
            ["Title"] = "DEMO Instructor draft via HTTP", ["Description"] = "Created through the real MVC endpoint",
            ["Price"] = "0", ["Level"] = "1", ["IsPublished"] = "true", ["InstructorId"] = Account("instructor-b").Email
        });
        Status(created, HttpStatusCode.Redirect);
        await using var db = session.OpenDb();
        var course = await db.Courses.SingleAsync(c => c.Title == "DEMO Instructor draft via HTTP");
        Require(!course.IsPublished && course.InstructorId == Account("instructor-a").Email, "Instructor forged publication or owner.");
        Require(await db.Courses.AnyAsync(c => c.Id == otherId && c.Title == "DEMO course b"), "Unauthorized edit/delete persisted.");
    }

    private async Task PublicationAsync()
    {
        using var guest = new DemoBrowser(session.BaseUrl);
        var before = await CourseFingerprintAsync();
        Require((await guest.HtmlAsync("/Courses")).Contains("href=\"" + CourseUrl + "\""), "Published course missing in DAO public list.");
        foreach (var published in new[] { false, true })
        {
            using (var changed = await admin.PostAsync("/Admin/" + (published ? "EnableCourse/" : "DisableCourse/") + CourseA, CourseForm)) Status(changed, HttpStatusCode.Redirect);
            DateTime? updated;
            await using (var db = session.OpenDb()) updated = await db.Courses.Where(c => c.Id == CourseA).Select(c => c.UpdatedAt).SingleAsync();
            using (var repeated = await admin.PostAsync("/Admin/" + (published ? "EnableCourse/" : "DisableCourse/") + CourseA, CourseForm)) Status(repeated, HttpStatusCode.Redirect);
            await using (var db = session.OpenDb()) Require(updated == await db.Courses.Where(c => c.Id == CourseA).Select(c => c.UpdatedAt).SingleAsync(), "Idempotent publication changed timestamp.");
            var publicList = await guest.HtmlAsync("/Courses");
            Require(publicList.Contains("href=\"" + CourseUrl + "\"") == published, "Public list did not follow publication.");
            using (var access = await guest.Client.GetAsync(CourseUrl)) Status(access, published ? HttpStatusCode.OK : HttpStatusCode.NotFound);
            foreach (var key in new[] { "student-active", "student-completed", "student-cancelled", "student-none", "instructor-a", "instructor-b", "admin-a" })
            {
                using var browser = await SignInAsync(key);
                using var access = await browser.Client.GetAsync(CourseUrl);
                var allowed = published || key is "student-active" or "student-completed" or "instructor-a" or "admin-a";
                Status(access, allowed ? HttpStatusCode.OK : HttpStatusCode.NotFound);
                Require(!(await access.Content.ReadAsStringAsync()).Contains("PRIVATE_ADMIN_DEMO_CONTENT"), "Lesson content leaked through public metadata.");
            }
            if (!published)
            {
                var fields = await EditFieldsAsync(CourseA); fields["IsPublished"] = "true";
                using var edited = await owner.PostAsync("/Courses/Edit/" + CourseA, "/Courses/Edit/" + CourseA, fields);
                Status(edited, HttpStatusCode.Redirect);
                await using var db = session.OpenDb(); Require(!await db.Courses.Where(c => c.Id == CourseA).Select(c => c.IsPublished).SingleAsync(), "Instructor re-enabled an Admin-hidden course.");
                Require((await admin.HtmlAsync(CourseForm)).Contains("PRIVATE_ADMIN_DEMO_CONTENT"), "Admin cannot read hidden lesson details.");
            }
            Require(before == await CourseFingerprintAsync(), "Publication changed course content, lessons or enrollment.");
            await DashboardAsync();
        }
    }

    private async Task MutationsAsync()
    {
        foreach (var path in new[] { "/Admin/DisableCourse/" + CourseA, "/Admin/DeactivateUser/" + Account("student-none").Id })
        {
            using var get = await admin.Client.GetAsync(path); Status(get, HttpStatusCode.MethodNotAllowed);
            using var noToken = await admin.Client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>())); Status(noToken, HttpStatusCode.BadRequest);
        }
        using (var missing = await admin.PostAsync("/Admin/DisableCourse/2147483647", CourseForm)) Status(missing, HttpStatusCode.NotFound);
        using (var missing = await admin.PostAsync("/Admin/DeactivateUser/not-a-demo-user", CourseForm)) Status(missing, HttpStatusCode.NotFound);
        using (var self = await admin.PostAsync("/Admin/DeactivateUser/" + Account("admin-a").Id, "/Admin/Users")) Status(self, HttpStatusCode.Redirect);
        await using (var db = session.OpenDb()) Require(await db.Users.Where(u => u.Id == Account("admin-a").Id).Select(u => u.IsActive).SingleAsync(), "Admin self-deactivated.");
        var id = Account("student-none").Id;
        using (var first = await admin.PostAsync("/Admin/DeactivateUser/" + id, "/Admin/Users")) Status(first, HttpStatusCode.Redirect);
        string? stamp;
        await using (var db = session.OpenDb()) stamp = await db.Users.Where(u => u.Id == id).Select(u => u.SecurityStamp).SingleAsync();
        using (var again = await admin.PostAsync("/Admin/DeactivateUser/" + id, "/Admin/Users")) Status(again, HttpStatusCode.Redirect);
        await using (var db = session.OpenDb()) Require(stamp == await db.Users.Where(u => u.Id == id).Select(u => u.SecurityStamp).SingleAsync(), "Idempotent deactivation changed stamp.");
        using (var restored = await admin.PostAsync("/Admin/ActivateUser/" + id, "/Admin/Users")) Status(restored, HttpStatusCode.Redirect);
    }

    private async Task ConcurrentAdminsAsync()
    {
        using var b = await SignInAsync("admin-b");
        var aId = Account("admin-a").Id; var bId = Account("admin-b").Id;
        // Obtain both tokens before dispatch so authorization can overlap.
        var aFields = new Dictionary<string, string> { ["__RequestVerificationToken"] = await admin.TokenAsync("/Admin/Users") };
        var bFields = new Dictionary<string, string> { ["__RequestVerificationToken"] = await b.TokenAsync("/Admin/Users") };
        var responses = await Task.WhenAll(admin.Client.PostAsync("/Admin/DeactivateUser/" + bId, new FormUrlEncodedContent(aFields)),
            b.Client.PostAsync("/Admin/DeactivateUser/" + aId, new FormUrlEncodedContent(bFields)));
        foreach (var response in responses) { using (response) Status(response, HttpStatusCode.Redirect); }
        await using var db = session.OpenDb();
        var activeAdmins = await (from user in db.Users join membership in db.UserRoles on user.Id equals membership.UserId
            join role in db.Roles on membership.RoleId equals role.Id where user.IsActive && role.NormalizedName == "ADMIN" select user.Id).ToListAsync();
        Require(activeAdmins.Count == 1, "Concurrent HTTP actions did not preserve exactly one demo Admin.");
        var survivor = activeAdmins.Single() == aId ? admin : b;
        var target = activeAdmins.Single() == aId ? bId : aId;
        using var restored = await survivor.PostAsync("/Admin/ActivateUser/" + target, "/Admin/Users"); Status(restored, HttpStatusCode.Redirect);
        await using var check = session.OpenDb();
        Require(await check.Users.Where(u => u.Id == aId || u.Id == bId).AllAsync(u => u.IsActive), "Demo Admins were not restored.");
        owner.Dispose();
    }

    private async Task DashboardAsync()
    {
        await using var db = session.OpenDb();
        var values = new Dictionary<string, int>
        {
            ["total-users"] = await db.Users.CountAsync(), ["active-users"] = await db.Users.CountAsync(u => u.IsActive),
            ["inactive-users"] = await db.Users.CountAsync(u => !u.IsActive), ["total-courses"] = await db.Courses.CountAsync(),
            ["published-courses"] = await db.Courses.CountAsync(c => c.IsPublished), ["hidden-courses"] = await db.Courses.CountAsync(c => !c.IsPublished),
            ["total-lessons"] = await db.Lessons.CountAsync()
        };
        var html = await admin.HtmlAsync("/Admin");
        foreach (var pair in values) Require(html.Contains("data-testid=\"" + pair.Key + "\">" + pair.Value.ToString("N0") + "</strong>"), "Dashboard/SQL mismatch: " + pair.Key);
    }

    private async Task<Dictionary<string, string>> EditFieldsAsync(int id)
    {
        await using var db = session.OpenDb(); var course = await db.Courses.AsNoTracking().SingleAsync(c => c.Id == id);
        return new() { ["Id"] = id.ToString(), ["Title"] = course.Title, ["Description"] = course.Description,
            ["Price"] = course.Price.ToString(System.Globalization.CultureInfo.InvariantCulture), ["Level"] = ((int)course.Level).ToString(), ["IsPublished"] = "true" };
    }

    private async Task<string> UserFingerprintAsync(string id)
    {
        await using var db = session.OpenDb();
        var value = await db.Users.Where(u => u.Id == id).Select(u => new { u.PasswordHash, u.FullName, u.Email, u.UserName,
            u.LockoutEnd, u.LockoutEnabled, u.AccessFailedCount }).SingleAsync();
        var roles = await db.UserRoles.Where(r => r.UserId == id).OrderBy(r => r.RoleId).Select(r => r.RoleId).ToListAsync();
        return Hash(new { value, roles }); // Do not persist authentication fields in reports.
    }

    private async Task<string> CourseFingerprintAsync()
    {
        await using var db = session.OpenDb();
        var course = await db.Courses.Where(c => c.Id == CourseA).Select(c => new { c.Title, c.Description, c.InstructorId, c.Price, c.Level, c.ThumbnailUrl }).SingleAsync();
        var lessons = await db.Lessons.Where(l => l.CourseId == CourseA).OrderBy(l => l.Id).Select(l => new { l.Id, l.Content, l.VideoUrl, l.Title, l.Order }).ToListAsync();
        var enrollments = await db.Enrollments.Where(e => e.CourseId == CourseA).OrderBy(e => e.Id).Select(e => new { e.Id, e.UserId, e.Status, e.EnrolledAt }).ToListAsync();
        return Hash(new { course, lessons, enrollments });
    }

    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));

}
