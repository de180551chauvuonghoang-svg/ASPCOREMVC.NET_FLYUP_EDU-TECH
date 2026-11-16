using System.Net;
using System.Text.RegularExpressions;
using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using EduFlyUp.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EduFlyUp.Tests;

public sealed class AdminUserTests
{
    private static FormUrlEncodedContent Form(string token, string? returnUrl = null) => new(new Dictionary<string, string>
    {
        ["__RequestVerificationToken"] = token, ["returnUrl"] = returnUrl ?? "https://example.com",
        ["Id"] = "forged", ["IsActive"] = "true", ["Role"] = "Admin", ["Email"] = "forged@example.test", ["Password"] = "forged"
    });

    [Fact]
    public async Task List_filters_at_database_pages_stably_and_loads_multiple_roles()
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("admin@example.test", "Admin");
        await app.CreateUserAsync("student@example.test", "Student");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var roleId = await db.Roles.Where(r => r.Name == "Student").Select(r => r.Id).SingleAsync();
            var adminRoleId = await db.Roles.Where(r => r.Name == "Admin").Select(r => r.Id).SingleAsync();
            for (var i = 1; i <= 24; i++)
            {
                var user = new ApplicationUser { Id = $"user-{i:D2}", FullName = "Needle " + i,
                    Email = $"needle{i}@example.test", UserName = $"needle{i}@example.test", IsActive = i != 24,
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
                db.Users.Add(user); db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = roleId });
            }
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = "user-11", RoleId = adminRoleId });
            await db.SaveChangesAsync();
        }
        using (var scope = app.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IAdminUserService>();
            var query = new AdminUserQuery(" Needle ", " student ", true, 2);
            var page = await service.SearchAsync(query);
            Assert.Equal(23, page.TotalCount); Assert.Equal(3, page.TotalPages);
            Assert.Equal(Enumerable.Range(11, 10).Select(i => $"user-{i:D2}"), page.Items.Select(u => u.Id));
            Assert.Equal(new[] { "Admin", "Student" }, page.Items[0].Roles);
            Assert.Equal(3, (await service.SearchAsync(query with { Page = int.MaxValue })).Page);
            Assert.Equal(1, (await service.SearchAsync(query with { Page = -1 })).Page);
            Assert.Equal("user-24", Assert.Single((await service.SearchAsync(query with { IsActive = false })).Items).Id);
            Assert.Empty((await service.SearchAsync(query with { Role = "unknown" })).Items);
            Assert.Single((await service.SearchAsync(new AdminUserQuery("needle24@example.test"))).Items);
        }
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "admin@example.test");
        var url = "/Admin/Users?Search=Needle&Role=Student&IsActive=true&Page=1";
        var response = await browser.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        var link = Regex.Matches(html, "href=\"([^\"]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).First(h => h.Contains("Page=2"));
        Assert.Contains("Search=Needle", link); Assert.Contains("Role=Student", link); Assert.Contains("IsActive=True", link);
        Assert.Contains("name=\"returnUrl\" value=\"" + url + "\"", html);
        Assert.Equal(HttpStatusCode.OK, (await browser.Client.GetAsync(link)).StatusCode);
    }

    [Fact]
    public async Task Detail_joins_courses_by_username_and_enrollments_by_user_id_without_authentication_fields()
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("admin@example.test", "Admin");
        var id = await app.CreateUserAsync("owner@example.test", "Instructor");
        string? hash = null; string? stamp = null;
        await app.ChangeUserAsync(id, async (users, user) =>
        {
            user.FullName = "Course owner"; user.Bio = "<script>alert('x')</script>";
            user.PhoneNumber = "0123456789"; user.AvatarUrl = "javascript:alert('x')";
            Assert.True((await users.UpdateAsync(user)).Succeeded); hash = user.PasswordHash; stamp = user.SecurityStamp;
        });
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Courses.AddRange(new Course { Id = 1, Title = "Owned by username", InstructorId = "OWNER@EXAMPLE.TEST" },
                new Course { Id = 2, Title = "Not owned via identity id", InstructorId = id });
            db.Enrollments.AddRange(new Enrollment { UserId = id, CourseId = 2, Status = EnrollmentStatus.Cancelled },
                new Enrollment { UserId = "owner@example.test", CourseId = 1, Status = EnrollmentStatus.Active });
            await db.SaveChangesAsync();
            var detail = await scope.ServiceProvider.GetRequiredService<IAdminUserService>().GetDetailAsync(id);
            Assert.NotNull(detail); Assert.Equal(1, Assert.Single(detail.Courses).Id);
            Assert.Equal(EnrollmentStatus.Cancelled, Assert.Single(detail.Enrollments).Status);
            Assert.Equal(2, detail.Enrollments.Single().CourseId);
        }
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "admin@example.test");
        var html = await (await browser.Client.GetAsync("/Admin/UserDetails/" + id)).Content.ReadAsStringAsync();
        Assert.Contains("&lt;script&gt;", html); Assert.DoesNotContain("<script>alert", html);
        Assert.DoesNotContain("href=\"javascript:", html); Assert.DoesNotContain("src=\"javascript:", html);
        Assert.Contains("0123456789", html); Assert.Contains("Cancelled", html);
        Assert.DoesNotContain(hash!, html); Assert.DoesNotContain(stamp!, html);
        Assert.DoesNotContain("PasswordHash", html); Assert.DoesNotContain("SecurityStamp", html);
    }

    [Theory]
    [InlineData(null)] [InlineData("Student")] [InlineData("Instructor")] [InlineData("Teacher")]
    public async Task User_endpoints_require_admin_for_reads_and_writes(string? role)
    {
        using var app = new TestApplication();
        var id = await app.CreateUserAsync("target@example.test", "Student");
        using var browser = app.CreateBrowser();
        if (role is not null)
        {
            await app.CreateUserAsync("actor@example.test", role);
            await TestApplication.LoginAsync(browser, "actor@example.test");
        }
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Account/Login");
        var responses = new[] { await browser.Client.GetAsync("/Admin/Users"), await browser.Client.GetAsync("/Admin/UserDetails/" + id),
            await browser.Client.PostAsync("/Admin/DeactivateUser/" + id, Form(token)), await browser.Client.PostAsync("/Admin/ActivateUser/" + id, Form(token)) };
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains(role is null ? "/Account/Login" : "/Account/AccessDenied", response.Headers.Location?.ToString());
        }
    }

    [Fact]
    public async Task Deactivate_reactivate_revokes_copied_cookies_and_preserves_roles_credentials_lockout_and_data()
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("admin@example.test", "Admin");
        var id = await app.CreateUserAsync("student@example.test", "Student");
        using var admin = app.CreateBrowser(); using var student = app.CreateBrowser();
        await TestApplication.LoginAsync(admin, "admin@example.test");
        await TestApplication.LoginAsync(student, "student@example.test");
        var copiedCookie = student.AuthCookie;
        Assert.NotNull(copiedCookie);
        var lockout = DateTimeOffset.UtcNow.AddHours(1);
        string? oldStamp = null; string? oldHash = null;
        await app.ChangeUserAsync(id, async (users, user) =>
        {
            user.LockoutEnd = lockout; user.AccessFailedCount = 2;
            Assert.True((await users.UpdateAsync(user)).Succeeded); oldStamp = user.SecurityStamp; oldHash = user.PasswordHash;
        });
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Courses.Add(new Course { Id = 1, Title = "Keep course", InstructorId = "student@example.test" });
            db.Enrollments.Add(new Enrollment { UserId = id, CourseId = 1, Status = EnrollmentStatus.Completed });
            await db.SaveChangesAsync();
        }
        var token = await TestApplication.AntiForgeryTokenAsync(admin, "/Admin/UserDetails/" + id);
        var path = "/Admin/Users?Role=Student&IsActive=true&Page=2";
        string? blockedStamp = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var response = await admin.Client.PostAsync("/Admin/DeactivateUser/" + id, Form(token, path));
            Assert.Equal(path, response.Headers.Location?.ToString());
            await app.ChangeUserAsync(id, (users, user) =>
            {
                Assert.False(user.IsActive); Assert.NotEqual(oldStamp, user.SecurityStamp);
                if (attempt == 0) blockedStamp = user.SecurityStamp; else Assert.Equal(blockedStamp, user.SecurityStamp);
                return Task.CompletedTask;
            });
        }
        var denied = await student.Client.GetAsync("/Instructor/Dashboard");
        Assert.Contains("/Account/Login", denied.Headers.Location?.ToString()); Assert.Null(student.AuthCookie);
        Assert.Equal(HttpStatusCode.OK, (await TestApplication.LoginAsync(student, "student@example.test")).StatusCode);
        for (var attempt = 0; attempt < 2; attempt++)
            Assert.Equal(HttpStatusCode.Redirect, (await admin.Client.PostAsync("/Admin/ActivateUser/" + id, Form(token))).StatusCode);
        using var copied = app.CreateBrowser(copiedCookie);
        Assert.Contains("/Account/Login", (await copied.Client.GetAsync("/Instructor/Dashboard")).Headers.Location?.ToString());
        await app.ChangeUserAsync(id, async (users, user) =>
        {
            Assert.True(user.IsActive); Assert.Equal(blockedStamp, user.SecurityStamp); Assert.Equal(oldHash, user.PasswordHash);
            Assert.Equal("student@example.test", user.Email); Assert.Equal(2, user.AccessFailedCount);
            Assert.Equal(lockout, user.LockoutEnd); Assert.Equal(new[] { "Student" }, await users.GetRolesAsync(user));
        });
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("Keep course", (await db.Courses.SingleAsync()).Title);
            Assert.Equal(EnrollmentStatus.Completed, (await db.Enrollments.SingleAsync()).Status);
        }
        Assert.Equal(HttpStatusCode.OK, (await TestApplication.LoginAsync(student, "student@example.test")).StatusCode);
        await app.ChangeUserAsync(id, async (users, user) => Assert.True((await users.SetLockoutEndDateAsync(user, null)).Succeeded));
        Assert.Equal(HttpStatusCode.Redirect, (await TestApplication.LoginAsync(student, "student@example.test")).StatusCode);
        Assert.NotNull(student.AuthCookie);
    }

    [Fact]
    public async Task Self_deactivation_is_rejected_and_mutations_require_post_token_and_valid_target()
    {
        using var app = new TestApplication();
        var id = await app.CreateUserAsync("admin@example.test", "Admin");
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "admin@example.test");
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Admin/UserDetails/" + id);
        foreach (var action in new[] { "ActivateUser", "DeactivateUser" })
        {
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (await browser.Client.GetAsync("/Admin/" + action + "/" + id)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await browser.Client.PostAsync("/Admin/" + action + "/" + id, Form(""))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await browser.Client.PostAsync("/Admin/" + action + "/missing", Form(token))).StatusCode);
        }
        var response = await browser.Client.PostAsync("/Admin/DeactivateUser/" + id, Form(token));
        Assert.Equal("/Admin/UserDetails/" + id, response.Headers.Location?.ToString());
        var html = WebUtility.HtmlDecode(await (await browser.Client.GetAsync(response.Headers.Location)).Content.ReadAsStringAsync());
        Assert.Contains("không thể tự khóa", html);
        await app.ChangeUserAsync(id, (users, user) => { Assert.True(user.IsActive); return Task.CompletedTask; });
        Assert.Equal(HttpStatusCode.NotFound, (await browser.Client.GetAsync("/Admin/UserDetails/missing")).StatusCode);
    }

    [Fact]
    public async Task Concurrent_cross_deactivation_rechecks_actor_after_waiting_instead_of_using_cached_identity()
    {
        using var app = new TestApplication();
        var a = await app.CreateUserAsync("a@example.test", "Admin"); var b = await app.CreateUserAsync("b@example.test", "Admin");
        using var first = app.Services.CreateScope(); using var second = app.Services.CreateScope();
        // Simulate cookie validation tracking both actors before either lock.
        await first.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(a);
        await second.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(b);
        var results = await Task.WhenAll(first.ServiceProvider.GetRequiredService<IAdminUserService>().SetActiveAsync(a, b, false),
            second.ServiceProvider.GetRequiredService<IAdminUserService>().SetActiveAsync(b, a, false));
        Assert.Contains(AdminUserChangeResult.Changed, results); Assert.Contains(AdminUserChangeResult.ActorNotAllowed, results);
        using var scope = app.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.CountAsync(u => u.IsActive));
    }

    [Fact]
    public async Task Actor_whose_admin_role_was_removed_cannot_mutate_using_old_role_claims()
    {
        using var app = new TestApplication();
        var actor = await app.CreateUserAsync("admin@example.test", "Admin");
        var target = await app.CreateUserAsync("student@example.test", "Student");
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "admin@example.test");
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Admin/UserDetails/" + target);
        await app.ChangeUserAsync(actor, async (users, user) => Assert.True((await users.RemoveFromRoleAsync(user, "Admin")).Succeeded));
        var response = await browser.Client.PostAsync("/Admin/DeactivateUser/" + target, Form(token));
        Assert.Contains("/Account/AccessDenied", response.Headers.Location?.ToString());
        await app.ChangeUserAsync(target, (users, user) => { Assert.True(user.IsActive); return Task.CompletedTask; });
    }

    [Theory]
    [InlineData("IsActive=wrong")] [InlineData("Page=wrong")]
    public async Task Invalid_filters_are_rejected(string query)
    {
        using var app = new TestApplication(); await app.CreateUserAsync("admin@example.test", "Admin");
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "admin@example.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Client.GetAsync("/Admin/Users?" + query)).StatusCode);
    }

    [Fact]
    public async Task Empty_profiles_and_empty_lists_have_clear_messages()
    {
        using var app = new TestApplication(); var id = await app.CreateUserAsync("admin@example.test", "Admin");
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "admin@example.test");
        var detail = WebUtility.HtmlDecode(await (await browser.Client.GetAsync("/Admin/UserDetails/" + id)).Content.ReadAsStringAsync());
        Assert.Contains("Chưa có giới thiệu.", detail); Assert.Contains("chưa tạo khóa học", detail); Assert.Contains("chưa có ghi danh", detail);
        var empty = WebUtility.HtmlDecode(await (await browser.Client.GetAsync("/Admin/Users?Search=not-found")).Content.ReadAsStringAsync());
        Assert.Contains("Không tìm thấy tài khoản", empty);
    }

    [Fact]
    public void User_filters_translate_to_sql_server_without_materializing_users_or_roles()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=unused;Database=TranslationOnly;Integrated Security=True").Options);
        var sql = AdminUserRepository.Filter(db, new AdminUserQuery("needle", "Instructor", false)).OrderBy(u => u.Id).Skip(10).Take(10)
            .Select(u => new { u.Id, u.FullName, u.Email, u.IsActive }).ToQueryString();
        Assert.Contains("EXISTS", sql); Assert.Contains("OFFSET", sql); Assert.Contains("FETCH NEXT", sql);
        Assert.Contains("[NormalizedName]", sql); Assert.DoesNotContain("[PasswordHash]", sql); Assert.DoesNotContain("[SecurityStamp]", sql);
    }

    [Fact]
    public async Task Failed_identity_write_is_reported_as_conflict_without_changing_account()
    {
        using var app = new TestApplication();
        var actor = await app.CreateUserAsync("admin@example.test", "Admin");
        var target = await app.CreateUserAsync("student@example.test", "Student");
        using var scope = app.Services.CreateScope();
        var service = new AdminUserService(new FailedWriteRepository(scope.ServiceProvider.GetRequiredService<IAdminUserRepository>()),
            scope.ServiceProvider.GetRequiredService<IAdminUserStatusLock>(), NullLogger<AdminUserService>.Instance);
        Assert.Equal(AdminUserChangeResult.Conflict, await service.SetActiveAsync(actor, target, false));
        await app.ChangeUserAsync(target, (users, user) => { Assert.True(user.IsActive); return Task.CompletedTask; });
    }

    [Fact]
    public async Task Production_guard_refuses_to_silently_run_without_sql_server_transactions()
    {
        using var app = new TestApplication(); using var scope = app.Services.CreateScope();
        var guard = new SqlServerAdminUserStatusLock(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => guard.BeginAsync());
    }

    private sealed class FailedWriteRepository(IAdminUserRepository inner) : IAdminUserRepository
    {
        public Task<AdminUserPage> SearchAsync(AdminUserQuery query) => inner.SearchAsync(query);
        public Task<AdminUserDetail?> GetDetailAsync(string id) => inner.GetDetailAsync(id);
        public void RefreshForStatusChange() => inner.RefreshForStatusChange();
        public Task<AdminUserStatus?> GetStatusAsync(string id) => inner.GetStatusAsync(id);
        public Task<int> CountActiveAdminsAsync() => inner.CountActiveAdminsAsync();
        public Task<bool> SaveActiveStateAsync(string id, bool active) => Task.FromResult(false);
    }
}
