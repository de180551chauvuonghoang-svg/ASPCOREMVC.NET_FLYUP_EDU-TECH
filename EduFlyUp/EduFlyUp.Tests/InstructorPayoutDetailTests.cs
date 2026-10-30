using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using EduFlyUp.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace EduFlyUp.Tests;

public sealed class InstructorPayoutDetailTests
{
    [Theory]
    [InlineData("Student")]
    [InlineData("Admin")]
    [InlineData("Teacher")]
    public async Task Detail_requires_instructor_role(string role)
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("reader@example.test", role);
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "reader@example.test");
        Assert.Equal(HttpStatusCode.Redirect, (await browser.Client.GetAsync("/InstructorPayouts/Details/1")).StatusCode);
        using var guest = app.CreateBrowser();
        Assert.Equal(HttpStatusCode.Redirect, (await guest.Client.GetAsync("/InstructorPayouts/Details/1")).StatusCode);
    }

    [Fact]
    public async Task Detail_protects_ownership_encodes_bank_and_notes_and_excludes_raw_accounts_and_submission_keys()
    {
        using var app = new TestApplication();
        var owner = await app.CreateUserAsync("owner@example.test", "Instructor");
        var key = Guid.NewGuid();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.PayoutRequests.AddRange(new PayoutRequest { Id = 1, InstructorId = owner, Amount = 123456.75m,
                    BankName = "<script>alert(1)</script>", AccountHolderName = "<b>Demo Holder</b>", AccountNumber = "0012345678",
                    SubmissionKey = key, AdminNote = "<img src=x onerror=alert(1)>", Status = PayoutStatus.Rejected,
                    CreatedAt = new DateTime(2026, 10, 31), ProcessedAt = new DateTime(2026, 10, 31, 1, 0, 0) },
                new PayoutRequest { Id = 2, InstructorId = "foreign", AccountNumber = "FOREIGN_ACCOUNT", AdminNote = "FOREIGN_NOTE" },
                new PayoutRequest { Id = 3, InstructorId = "owner@example.test", Amount = 200000 });
            await db.SaveChangesAsync();
        }
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "owner@example.test");
        var response = await browser.Client.GetAsync("/InstructorPayouts/Details/1?id=2&instructorId=foreign");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("123.456,75", html); Assert.Contains("•••• 5678", WebUtility.HtmlDecode(html));
        Assert.Contains("31/10/2026 07:00", html); Assert.Contains("31/10/2026 08:00", html);
        Assert.Contains("Bị từ chối", WebUtility.HtmlDecode(html));
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html); Assert.Contains("&lt;b&gt;Demo Holder&lt;/b&gt;", html);
        foreach (var forbidden in new[] { "0012345678", key.ToString(), "<script>alert(1)</script>", "<img src=x onerror=alert(1)>", "FOREIGN_ACCOUNT", "FOREIGN_NOTE" })
            Assert.DoesNotContain(forbidden, html);
        var legacy = WebUtility.HtmlDecode(await browser.Client.GetStringAsync("/InstructorPayouts/Details/3"));
        Assert.Contains("Chưa có phản hồi từ Admin.", legacy); Assert.Contains("Chưa có thông tin", legacy);
        foreach (var id in new[] { "2", "999" }) Assert.Equal(HttpStatusCode.NotFound, (await browser.Client.GetAsync("/InstructorPayouts/Details/" + id)).StatusCode);
        foreach (var id in new[] { "0", "-1", "wrong" }) Assert.Equal(HttpStatusCode.BadRequest, (await browser.Client.GetAsync("/InstructorPayouts/Details/" + id)).StatusCode);
    }

    [SqlServerFact]
    public async Task Detail_is_owned_and_returns_only_masked_account_numbers_including_short_legacy_values()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var requests = new[]
        {
            new PayoutRequest { InstructorId = "owner-id", Amount = 123456.78m, BankName = "Demo Bank", AccountNumber = "0012345678", AccountHolderName = "Demo Holder" },
            new PayoutRequest { InstructorId = "owner@example.test", Amount = 200000, AccountNumber = "1234", Status = PayoutStatus.Rejected },
            new PayoutRequest { InstructorId = "owner-id", Amount = 100000, AccountNumber = "" },
            new PayoutRequest { InstructorId = "foreign", Amount = 999999, AccountNumber = "FOREIGN_ACCOUNT" }
        };
        db.PayoutRequests.AddRange(requests); await db.SaveChangesAsync();
        var service = new InstructorPayoutService(new InstructorPayoutRepository(db));
        var actor = new InstructorIdentity("owner-id", "owner@example.test");
        var first = await service.DetailAsync(actor, requests[0].Id);
        Assert.NotNull(first); Assert.Equal("•••• 5678", first.MaskedAccountNumber);
        Assert.Equal(123456.78m, first.Request.Amount); Assert.Equal("Demo Bank", first.BankName); Assert.Equal("Demo Holder", first.AccountHolderName);
        Assert.Equal("••••", (await service.DetailAsync(actor, requests[1].Id))!.MaskedAccountNumber);
        Assert.Equal("Chưa có thông tin", (await service.DetailAsync(actor, requests[2].Id))!.MaskedAccountNumber);
        Assert.Null(await service.DetailAsync(actor, requests[3].Id));
        Assert.Null(await service.DetailAsync(actor, int.MaxValue)); Assert.Null(await service.DetailAsync(actor, 0));
        Assert.Null(await service.DetailAsync(new("", "owner@example.test"), requests[0].Id));
    }
}
