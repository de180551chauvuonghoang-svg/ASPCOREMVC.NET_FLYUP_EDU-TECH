using System.Net;
using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;

namespace EduFlyUp.Tests;

public sealed class InstructorRevenueHttpTests
{
    [Theory]
    [InlineData("Student")]
    [InlineData("Admin")]
    [InlineData("Teacher")]
    public async Task Revenue_is_only_available_to_instructors(string role)
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("reader@example.test", role);
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "reader@example.test");
        Assert.Equal(HttpStatusCode.Redirect, (await browser.Client.GetAsync("/InstructorRevenue")).StatusCode);
        using var guest = app.CreateBrowser(); Assert.Equal(HttpStatusCode.Redirect, (await guest.Client.GetAsync("/InstructorRevenue")).StatusCode);
    }

    [Fact]
    public async Task Unverified_source_is_not_displayed_as_zero_income_and_foreign_rows_are_not_disclosed()
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "owner@example.test");
        var response = await browser.Client.GetAsync("/InstructorRevenue"); var html = await response.Content.ReadAsStringAsync();
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains("Chưa có dữ liệu doanh thu đã xác minh", WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("id=\"revenue-summary\"", html);
        using var bad = new TestApplication(settlements: new InstructorSettlementEarningsTests.TestSettlements([
            new("FOREIGN_RECEIPT", "foreign", 1000000, 0)]));
        await bad.CreateUserAsync("owner@example.test", "Instructor");
        using var otherBrowser = bad.CreateBrowser(); await TestApplication.LoginAsync(otherBrowser, "owner@example.test");
        Assert.DoesNotContain("FOREIGN_RECEIPT", await otherBrowser.Client.GetStringAsync("/InstructorRevenue"));
    }

    [Fact]
    public async Task Verified_report_uses_session_owner_encodes_snapshots_pages_and_shows_actual_payment_shares()
    {
        var source = new ReportSource();
        using var app = new TestApplication(settlements: source);
        var owner = await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "owner@example.test");
        var response = await browser.Client.GetAsync("/InstructorRevenue?instructorId=foreign");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(owner, source.Actor?.UserId);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("630.000,00", html); Assert.Contains("150.000,00", html); Assert.Contains("100.000,00", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html); Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;b&gt;Student&lt;/b&gt;", html); Assert.DoesNotContain("<b>Student</b>", html);
        Assert.Contains("01/11/2026 07:00", html);
        Assert.Contains("Tổng cộng 12 khoản thu", WebUtility.HtmlDecode(html));
        var last = WebUtility.HtmlDecode(await browser.Client.GetStringAsync("/InstructorRevenue?page=999"));
        Assert.Contains("Trang 2/2", last);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Client.GetAsync("/InstructorRevenue?page=wrong")).StatusCode);
        var matched = WebUtility.HtmlDecode(await browser.Client.GetStringAsync("/InstructorRevenue?search=RECEIPT-11&page=999"));
        Assert.Contains("Tổng cộng 1 khoản thu", matched); Assert.Contains("52.500,00", matched);
        var searched = await browser.Client.GetStringAsync("/InstructorRevenue?search=STUDENT");
        Assert.Contains("href=\"/InstructorRevenue?page=2&amp;search=STUDENT\"", searched);
        Assert.Contains("Trang 2/2", WebUtility.HtmlDecode(await browser.Client.GetStringAsync("/InstructorRevenue?page=2&search=STUDENT")));
        Assert.Contains("Không tìm thấy khoản thu phù hợp", WebUtility.HtmlDecode(await browser.Client.GetStringAsync("/InstructorRevenue?search=nomatch")));
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Client.GetAsync("/InstructorRevenue?search=" + new string('a', 101))).StatusCode);
    }

    internal sealed class ReportSource : IInstructorSettlementSource
    {
        public InstructorIdentity? Actor { get; private set; }
        public Task<IReadOnlyList<InstructorSettledReceipt>?> ReadVerifiedReceiptsAsync(AppDbContext db, InstructorIdentity instructor)
        {
            Actor = instructor;
            IReadOnlyList<InstructorSettledReceipt> rows = Enumerable.Range(0, 12).Select(i => new InstructorSettledReceipt($"receipt-{i:D2}", instructor.UserId,
                100000, 25000, new(1, "<script>alert(1)</script>", "<b>Student</b>", 150000, new DateTime(2026, 11, 1)))).ToArray();
            return Task.FromResult<IReadOnlyList<InstructorSettledReceipt>?>(rows);
        }
    }
}
