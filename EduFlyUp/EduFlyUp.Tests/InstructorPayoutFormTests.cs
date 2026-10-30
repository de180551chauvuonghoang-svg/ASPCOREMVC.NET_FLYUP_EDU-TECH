using System.Net;
using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlyUp.Tests;

public sealed class InstructorPayoutFormTests
{
    private static Dictionary<string, string> Form(string token) => new()
    {
        ["__RequestVerificationToken"] = token, ["Amount"] = "100000.25", ["BankName"] = "Demo Bank",
        ["AccountNumber"] = "0012345678", ["AccountHolderName"] = "DUONG THAI DUY",
        ["InstructorId"] = "foreign", ["Status"] = "Approved", ["Balance.CreditedEarnings"] = "999999999", ["AdminNote"] = "FORGED_NOTE"
    };

    [Fact]
    public async Task Unknown_balance_hides_bank_form_and_forged_post_cannot_create_request()
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "owner@example.test");
        var response = await browser.Client.GetAsync("/InstructorPayouts/Create");
        var html = await response.Content.ReadAsStringAsync();
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains("Chưa xác minh", WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("name=\"AccountNumber\"", html);
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/InstructorPayouts/Create");
        var posted = await browser.Client.PostAsync("/InstructorPayouts/Create", new FormUrlEncodedContent(Form(token)));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, posted.StatusCode);
        Assert.DoesNotContain("0012345678", await posted.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Client.PostAsync("/InstructorPayouts/Create", new FormUrlEncodedContent(Form("")))).StatusCode);
        var invalid = Form(token); invalid["Amount"] = "-1";
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Client.PostAsync("/InstructorPayouts/Create", new FormUrlEncodedContent(invalid))).StatusCode);
        using var scope = app.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<AppDbContext>().PayoutRequests.ToListAsync());
    }

    [Fact]
    public async Task Verified_balance_form_posts_only_allowed_input_under_session_identity_and_redirects_to_history()
    {
        var service = new ReadyPayoutService();
        using var app = new TestApplication(payoutService: service);
        var owner = await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser(); await TestApplication.LoginAsync(browser, "owner@example.test");
        var html = await browser.Client.GetStringAsync("/InstructorPayouts/Create");
        Assert.Contains("name=\"AccountNumber\"", html); Assert.Contains("700.000,00 VNĐ", WebUtility.HtmlDecode(html));
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/InstructorPayouts/Create");
        var response = await browser.Client.PostAsync("/InstructorPayouts/Create", new FormUrlEncodedContent(Form(token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/InstructorPayouts", response.Headers.Location?.ToString());
        Assert.Equal(owner, service.Actor!.UserId); Assert.Equal("owner@example.test", service.Actor.UserName);
        Assert.Equal(100000.25m, service.Input!.Amount); Assert.Equal("0012345678", service.Input.AccountNumber);
    }

    private sealed class ReadyPayoutService : IInstructorPayoutService
    {
        public InstructorIdentity? Actor { get; private set; }
        public InstructorPayoutInput? Input { get; private set; }
        public Task<InstructorPayoutBalance> BalanceAsync(InstructorIdentity instructor) => Task.FromResult(new InstructorPayoutBalance(700000, 0, 0, 0));
        public Task<InstructorPayoutResult> CreateAsync(InstructorIdentity instructor, InstructorPayoutInput input)
        {
            Actor = instructor; Input = input;
            return Task.FromResult(new InstructorPayoutResult(InstructorPayoutChange.Created, "Đã gửi yêu cầu."));
        }
        public Task<InstructorPayoutHistory> HistoryAsync(InstructorIdentity instructor, int page, PayoutStatus? status = null)
            => Task.FromResult(new InstructorPayoutHistory([], 1, 0, status));
    }
}
