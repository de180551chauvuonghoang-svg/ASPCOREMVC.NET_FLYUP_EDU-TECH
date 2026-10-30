using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using EduFlyUp.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace EduFlyUp.Tests;

public sealed class InstructorPayoutTests
{
    [Theory]
    [InlineData("Student")]
    [InlineData("Admin")]
    [InlineData("Teacher")]
    public async Task History_requires_instructor_role(string role)
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("reader@example.test", role);
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "reader@example.test");
        Assert.Equal(HttpStatusCode.Redirect, (await browser.Client.GetAsync("/InstructorPayouts")).StatusCode);
        using var guest = app.CreateBrowser();
        Assert.Equal(HttpStatusCode.Redirect, (await guest.Client.GetAsync("/InstructorPayouts")).StatusCode);
    }

    [Fact]
    public async Task History_renders_own_requests_encodes_notes_and_never_exposes_bank_details()
    {
        using var app = new TestApplication();
        var owner = await app.CreateUserAsync("owner@example.test", "Instructor");
        await app.CreateUserAsync("empty@example.test", "Instructor");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 12; i++) db.PayoutRequests.Add(new PayoutRequest { InstructorId = i % 2 == 0 ? owner : "owner@example.test",
                Amount = 123456.75m, Status = (PayoutStatus)(i % 3), CreatedAt = new DateTime(2026, 10, 31),
                ProcessedAt = i % 3 == 0 ? null : new DateTime(2026, 10, 31, 1, 0, 0),
                AdminNote = "<script>alert(1)</script>", AccountNumber = "PRIVATE_ACCOUNT", AccountHolderName = "PRIVATE_HOLDER" });
            db.PayoutRequests.Add(new PayoutRequest { InstructorId = "foreign", AdminNote = "FOREIGN_NOTE" });
            await db.SaveChangesAsync();
        }
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "owner@example.test");
        var response = await browser.Client.GetAsync("/InstructorPayouts?instructorId=foreign");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("123.456,75", html); Assert.Contains("31/10/2026 07:00", html); Assert.Contains("31/10/2026 08:00", html);
        Assert.Contains("Tổng cộng 12 yêu cầu", WebUtility.HtmlDecode(html));
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html); Assert.DoesNotContain("<script>alert(1)</script>", html);
        foreach (var secret in new[] { "FOREIGN_NOTE", "PRIVATE_ACCOUNT", "PRIVATE_HOLDER" }) Assert.DoesNotContain(secret, html);
        var last = WebUtility.HtmlDecode(await browser.Client.GetStringAsync("/InstructorPayouts?page=999"));
        Assert.Contains("Trang 2/2", last);
        var filteredHtml = WebUtility.HtmlDecode(await browser.Client.GetStringAsync("/InstructorPayouts?status=Rejected&page=999"));
        Assert.Contains("Tổng cộng 4 yêu cầu", filteredHtml); Assert.Contains("Trang 1/1", filteredHtml);
        foreach (var query in new[] { "status=999", "status=wrong" })
            Assert.Equal(HttpStatusCode.BadRequest, (await browser.Client.GetAsync("/InstructorPayouts?" + query)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Client.GetAsync("/InstructorPayouts?page=wrong")).StatusCode);
        using var empty = app.CreateBrowser(); await TestApplication.LoginAsync(empty, "empty@example.test");
        Assert.Contains("Chưa có yêu cầu rút tiền", WebUtility.HtmlDecode(await empty.Client.GetStringAsync("/InstructorPayouts")));
        Assert.Contains("Không có yêu cầu phù hợp", WebUtility.HtmlDecode(await empty.Client.GetStringAsync("/InstructorPayouts?status=Pending")));
    }

    [SqlServerFact]
    public async Task History_is_scoped_to_identity_or_legacy_username_and_paged_at_sql_server()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var at = new DateTime(2026, 10, 31, 0, 0, 0);
        for (var i = 0; i < 12; i++) db.PayoutRequests.Add(new PayoutRequest
        {
            InstructorId = i % 2 == 0 ? "owner-id" : "owner@example.test", Amount = 100000 + i,
            CreatedAt = at, Status = (PayoutStatus)(i % 3), AccountNumber = "PRIVATE_ACCOUNT", AccountHolderName = "PRIVATE_HOLDER"
        });
        db.PayoutRequests.Add(new PayoutRequest { InstructorId = "foreign", Amount = 999999, AdminNote = "FOREIGN_NOTE" });
        await db.SaveChangesAsync();
        var service = new InstructorPayoutService(new InstructorPayoutRepository(db));
        var actor = new InstructorIdentity("owner-id", "owner@example.test");
        var first = await service.HistoryAsync(actor, 1);
        var last = await service.HistoryAsync(actor, int.MaxValue);
        Assert.Equal(12, first.TotalCount); Assert.Equal(10, first.Items.Count);
        Assert.Equal(2, last.Page); Assert.Equal(2, last.Items.Count);
        Assert.Equal(Enumerable.Range(0, 12).Reverse().Select(i => (decimal)(100000 + i)), first.Items.Concat(last.Items).Select(p => p.Amount));
        Assert.DoesNotContain(first.Items.Concat(last.Items), p => p.AdminNote == "FOREIGN_NOTE");
        foreach (var status in Enum.GetValues<PayoutStatus>())
        {
            var filtered = await service.HistoryAsync(actor, int.MaxValue, status);
            Assert.Equal(4, filtered.TotalCount); Assert.Equal(1, filtered.Page); Assert.Equal(status, filtered.StatusFilter);
            Assert.All(filtered.Items, item => Assert.Equal(status, item.Status));
        }
        Assert.Empty((await service.HistoryAsync(new("empty", "empty@example.test"), 1)).Items);
        Assert.Empty((await service.HistoryAsync(new("", "owner@example.test"), 1)).Items);
    }
}
