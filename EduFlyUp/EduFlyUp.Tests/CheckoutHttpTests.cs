using System.Net;
using System.Text.RegularExpressions;
using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Services;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlyUp.Tests;

public sealed class CheckoutHttpTests
{
    [SqlServerFact]
    public async Task Checkout_requires_student_and_get_preview_does_not_create_orders()
    {
        await using var r = await Rig.CreateAsync();
        using var guest = r.App.CreateBrowser();
        Assert.Equal(HttpStatusCode.Redirect, (await guest.Client.GetAsync($"/Checkout/Create?courseId={r.CourseId}")).StatusCode);
        await r.App.CreateUserAsync("instructor@example.test", "Instructor");
        using var other = r.App.CreateBrowser(); await TestApplication.LoginAsync(other, "instructor@example.test");
        Assert.Equal(HttpStatusCode.Redirect, (await other.Client.GetAsync($"/Checkout/Create?courseId={r.CourseId}")).StatusCode);
        var response = await r.Browser.Client.GetAsync($"/Checkout/Create?courseId={r.CourseId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(0, await r.Db.CheckoutOrders.CountAsync());
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await r.Browser.Client.GetAsync($"/Checkout/ApplyCoupon/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await r.Browser.Client.GetAsync("/Checkout/Create?courseId=wrong")).StatusCode);
    }

    [SqlServerFact]
    public async Task Creation_rejects_missing_token_ignores_posted_money_owner_and_status_and_replays_key()
    {
        await using var r = await Rig.CreateAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await r.Browser.Client.PostAsync("/Checkout/Create", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        var html = await r.Browser.Client.GetStringAsync($"/Checkout/Create?courseId={r.CourseId}");
        var fields = new Dictionary<string, string> { ["__RequestVerificationToken"] = Hidden(html, "__RequestVerificationToken"),
            ["SubmissionKey"] = Hidden(html, "SubmissionKey"), ["CourseId"] = r.CourseId.ToString(), ["BuyerId"] = "foreign",
            ["PayableAmount"] = "0", ["Price"] = "0", ["Status"] = "Paid" };
        var response = await r.Browser.Client.PostAsync("/Checkout/Create", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var order = await r.Db.CheckoutOrders.AsNoTracking().SingleAsync();
        Assert.Equal(r.Buyer, order.BuyerId); Assert.Equal(200000, order.PayableAmount); Assert.Equal(CheckoutOrderStatus.Pending, order.Status);
        Assert.Equal(HttpStatusCode.Redirect, (await r.Browser.Client.PostAsync("/Checkout/Create", new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Equal(1, await r.Db.CheckoutOrders.CountAsync()); Assert.Empty(await r.Db.Enrollments.ToListAsync());
        var detail = await r.Browser.Client.GetAsync(response.Headers.Location); var text = await detail.Content.ReadAsStringAsync();
        Assert.True(detail.Headers.CacheControl?.NoStore); Assert.Contains("200.000", text);
        Assert.Contains("&lt;script&gt;course&lt;/script&gt;", text); Assert.DoesNotContain("<script>course</script>", text);
        Assert.Contains("Chưa mở thanh toán", WebUtility.HtmlDecode(text));
        // Mở lại từ trang khóa học bằng key mới cũng về chính đơn Pending hiện có.
        var next = await r.Browser.Client.GetStringAsync($"/Checkout/Create?courseId={r.CourseId}");
        fields["SubmissionKey"] = Hidden(next, "SubmissionKey"); fields["__RequestVerificationToken"] = Hidden(next, "__RequestVerificationToken");
        Assert.Equal(response.Headers.Location, (await r.Browser.Client.PostAsync("/Checkout/Create", new FormUrlEncodedContent(fields))).Headers.Location);
    }

    [SqlServerFact]
    public async Task Coupon_forms_scope_route_validate_token_and_refresh_version_after_stale_post()
    {
        await using var r = await Rig.CreateAsync(); var url = await r.CreateOrder();
        var page = await r.Browser.Client.GetStringAsync(url); var oldVersion = Hidden(page, "Version");
        var id = (await r.Db.CheckoutOrders.AsNoTracking().SingleAsync()).PublicId;
        var fields = new Dictionary<string, string> { ["__RequestVerificationToken"] = Hidden(page, "__RequestVerificationToken"),
            ["Version"] = oldVersion, ["Code"] = "demo10", ["OrderPublicId"] = Guid.NewGuid().ToString(), ["BuyerId"] = "foreign" };
        Assert.Equal(HttpStatusCode.BadRequest, (await r.Browser.Client.PostAsync($"/Checkout/ApplyCoupon/{id}", new FormUrlEncodedContent(new Dictionary<string, string> { ["Code"] = "DEMO10", ["Version"] = oldVersion }))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await r.Browser.Client.PostAsync($"/Checkout/ApplyCoupon/{id}", new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Equal(180000, (await r.Db.CheckoutOrders.AsNoTracking().SingleAsync()).PayableAmount);
        var stale = await r.Browser.Client.PostAsync($"/Checkout/ApplyCoupon/{id}", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var refreshed = await stale.Content.ReadAsStringAsync(); Assert.NotEqual(oldVersion, Hidden(refreshed, "Version"));
        fields["Version"] = Hidden(refreshed, "Version"); fields["Code"] = "";
        Assert.Equal(HttpStatusCode.Redirect, (await r.Browser.Client.PostAsync($"/Checkout/ApplyCoupon/{id}", new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Equal(200000, (await r.Db.CheckoutOrders.AsNoTracking().SingleAsync()).PayableAmount);
        Assert.Equal(0, (await r.Db.Coupons.AsNoTracking().SingleAsync()).UsedCount);
    }

    [SqlServerFact]
    public async Task Foreign_missing_and_expired_orders_do_not_expose_edit_forms_or_allow_mutation()
    {
        await using var r = await Rig.CreateAsync(); var url = await r.CreateOrder();
        await r.App.CreateUserAsync("other@example.test", "Student"); using var other = r.App.CreateBrowser();
        await TestApplication.LoginAsync(other, "other@example.test");
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync(url+"?buyerId="+r.Buyer)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await r.Browser.Client.GetAsync($"/Checkout/Details/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await r.Browser.Client.GetAsync("/Checkout/Details/wrong")).StatusCode);
        var html = await r.Browser.Client.GetStringAsync(url); var order = await r.Db.CheckoutOrders.AsNoTracking().SingleAsync();
        var token = await TestApplication.AntiForgeryTokenAsync(other, $"/Checkout/Create?courseId={r.CourseId}");
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.PostAsync($"/Checkout/ApplyCoupon/{order.PublicId}", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["__RequestVerificationToken"] = token, ["Version"] = Hidden(html, "Version"), ["Code"] = "DEMO10" }))).StatusCode);
        await r.Db.CheckoutOrders.ExecuteUpdateAsync(s => s.SetProperty(o => o.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        var expired = await r.Browser.Client.GetStringAsync(url);
        Assert.Contains("Đơn đã hết hạn", WebUtility.HtmlDecode(expired)); Assert.DoesNotContain("checkout-code", expired);
    }

    [SqlServerFact]
    public async Task Free_checkout_grants_enrollment_and_displays_success_without_coupon_or_payment()
    {
        await using var r = await Rig.CreateAsync();
        await r.Db.Courses.ExecuteUpdateAsync(s => s.SetProperty(c => c.Price, 0m));
        var url = await r.CreateOrder(); var page = await r.Browser.Client.GetStringAsync(url);
        Assert.Contains("Đã đăng ký miễn phí", WebUtility.HtmlDecode(page)); Assert.DoesNotContain("checkout-code", page);
        Assert.Equal(r.Buyer, (await r.Db.Enrollments.AsNoTracking().SingleAsync()).UserId);
        Assert.Empty(await r.Db.PaymentTransactions.ToListAsync());
    }

    private static BankTransferOptions DemoBank() => new() { Enabled = true, BankBin = "970415", AccountNumber = "000000123456", AccountName = "DEMO ACCOUNT", QrTemplate = "qr_only" };

    [SqlServerFact]
    public async Task Qr_is_opt_in_post_only_and_does_not_prepare_on_get_or_without_antiforgery()
    {
        await using var r = await Rig.CreateAsync(); var url = await r.CreateOrder();
        var order = await r.Db.CheckoutOrders.AsNoTracking().SingleAsync();
        var endpoint = $"/Checkout/PreparePayment/{order.PublicId}";
        var page = await r.Browser.Client.GetStringAsync(url);
        Assert.DoesNotContain("Tạo QR thanh toán", WebUtility.HtmlDecode(page));
        Assert.DoesNotContain("img.vietqr.io", page); Assert.Empty(await r.Db.CheckoutPaymentIntents.ToListAsync());
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await r.Browser.Client.GetAsync(endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await r.Browser.Client.PostAsync(endpoint, new FormUrlEncodedContent(new Dictionary<string, string> { ["Version"] = Hidden(page, "Version") }))).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await r.Browser.Client.PostAsync(endpoint, new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Version"] = Hidden(page, "Version"), ["__RequestVerificationToken"] = Hidden(page, "__RequestVerificationToken") }))).StatusCode);
        Assert.Empty(await r.Db.CheckoutPaymentIntents.ToListAsync());
    }

    [SqlServerFact]
    public async Task Qr_uses_sealed_discounted_amount_ignores_posted_bank_and_replays_without_confirming_payment()
    {
        await using var r = await Rig.CreateAsync(DemoBank()); var url = await r.CreateOrder();
        var page = await r.Browser.Client.GetStringAsync(url); var oldVersion = Hidden(page, "Version");
        var order = await r.Db.CheckoutOrders.AsNoTracking().SingleAsync(); var endpoint = $"/Checkout/PreparePayment/{order.PublicId}";
        var fields = new Dictionary<string, string> { ["Version"] = oldVersion, ["__RequestVerificationToken"] = Hidden(page, "__RequestVerificationToken"), ["Code"] = "DEMO10" };
        Assert.Equal(HttpStatusCode.Redirect, (await r.Browser.Client.PostAsync($"/Checkout/ApplyCoupon/{order.PublicId}", new FormUrlEncodedContent(fields))).StatusCode);
        var stale = await r.Browser.Client.PostAsync(endpoint, new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); Assert.Empty(await r.Db.CheckoutPaymentIntents.ToListAsync());
        page = await stale.Content.ReadAsStringAsync(); fields["Version"] = Hidden(page, "Version");
        fields["Amount"] = "1"; fields["BankBin"] = "999999"; fields["AccountNumber"] = "ATTACKER"; fields["Reference"] = "FORGED"; fields["BuyerId"] = "foreign";
        var prepared = await r.Browser.Client.PostAsync(endpoint, new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, prepared.StatusCode);
        var intent = await r.Db.CheckoutPaymentIntents.AsNoTracking().SingleAsync();
        Assert.Equal(180000m, intent.Amount); Assert.Equal("000000123456", intent.AccountNumber);
        page = await r.Browser.Client.GetStringAsync(url);
        Assert.Contains($"https://img.vietqr.io/image/970415-000000123456-qr_only.svg?amount=180000&amp;addInfo={intent.Reference}&amp;accountName=DEMO%20ACCOUNT", page);
        Assert.Contains("referrerpolicy=\"no-referrer\"", page); Assert.DoesNotContain("checkout-code", page);
        var settings = r.App.Services.GetRequiredService<IOptions<BankTransferOptions>>().Value; settings.AccountNumber = "NEWACCOUNT";
        Assert.Equal(HttpStatusCode.Redirect, (await r.Browser.Client.PostAsync(endpoint, new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Contains("970415-000000123456-qr_only.svg", await r.Browser.Client.GetStringAsync(url));
        Assert.Equal(1, await r.Db.CheckoutPaymentIntents.CountAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await r.Browser.Client.PostAsync($"/Checkout/ApplyCoupon/{order.PublicId}", new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Empty(await r.Db.PaymentTransactions.ToListAsync()); Assert.Empty(await r.Db.Enrollments.ToListAsync());
        Assert.Equal(0, (await r.Db.Coupons.AsNoTracking().SingleAsync()).UsedCount);
        Assert.Equal(CheckoutOrderStatus.Pending, (await r.Db.CheckoutOrders.AsNoTracking().SingleAsync()).Status);
        settings.Enabled = false; Assert.DoesNotContain("img.vietqr.io", await r.Browser.Client.GetStringAsync(url));
    }

    [SqlServerFact]
    public async Task Qr_is_owner_scoped_and_hidden_for_expired_or_received_orders()
    {
        await using var r = await Rig.CreateAsync(DemoBank()); var url = await r.CreateOrder();
        var page = await r.Browser.Client.GetStringAsync(url); var order = await r.Db.CheckoutOrders.AsNoTracking().SingleAsync();
        var fields = new Dictionary<string, string> { ["Version"] = Hidden(page, "Version"), ["__RequestVerificationToken"] = Hidden(page, "__RequestVerificationToken") };
        var endpoint = $"/Checkout/PreparePayment/{order.PublicId}";
        await r.App.CreateUserAsync("foreign@example.test", "Student"); using var other = r.App.CreateBrowser();
        await TestApplication.LoginAsync(other, "foreign@example.test");
        var foreignFields = new Dictionary<string, string>(fields) { ["__RequestVerificationToken"] = await TestApplication.AntiForgeryTokenAsync(other, $"/Checkout/Create?courseId={r.CourseId}") };
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.PostAsync(endpoint, new FormUrlEncodedContent(foreignFields))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync(url)).StatusCode);
        Assert.Empty(await r.Db.CheckoutPaymentIntents.ToListAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await r.Browser.Client.PostAsync(endpoint, new FormUrlEncodedContent(fields))).StatusCode);
        await r.Db.CheckoutOrders.ExecuteUpdateAsync(s => s.SetProperty(o => o.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        page = await r.Browser.Client.GetStringAsync(url); Assert.DoesNotContain("img.vietqr.io", page); Assert.DoesNotContain("PreparePayment", page);
        Assert.Equal(HttpStatusCode.Conflict, (await r.Browser.Client.PostAsync(endpoint, new FormUrlEncodedContent(fields))).StatusCode);
        await r.Db.CheckoutOrders.ExecuteUpdateAsync(s => s.SetProperty(o => o.ExpiresAtUtc, order.ExpiresAtUtc));
        r.Db.PaymentTransactions.Add(new() { CheckoutOrderId = order.Id, Provider = "Test", ProviderTransactionId = "received-only", Amount = order.PayableAmount, OccurredAtUtc = DateTime.UtcNow });
        await r.Db.SaveChangesAsync();
        page = await r.Browser.Client.GetStringAsync(url); Assert.DoesNotContain("img.vietqr.io", page); Assert.DoesNotContain("PreparePayment", page);
    }

    private static string Hidden(string html, string name)
    {
        var match = Regex.Match(html, "name=\""+name+"\"[^>]*value=\"([^\"]*)\"");
        Assert.True(match.Success, name); return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    private sealed class Rig(AdminUserSqlServerTests.SqlDatabase data, IServiceScope scope, TestApplication app, TestBrowser browser) : IAsyncDisposable
    {
        public AppDbContext Db => scope.ServiceProvider.GetRequiredService<AppDbContext>();
        public TestApplication App => app; public TestBrowser Browser => browser;
        public string Buyer = ""; public int CourseId;
        public static async Task<Rig> CreateAsync(BankTransferOptions? bankTransfer = null)
        {
            var data = await AdminUserSqlServerTests.SqlDatabase.CreateAsync(); var scope = data.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var app = new TestApplication(sqlConnection: db.Database.GetConnectionString(), bankTransfer: bankTransfer); var browser = app.CreateBrowser();
            var rig = new Rig(data, scope, app, browser);
            rig.Buyer = await app.CreateUserAsync("buyer@example.test", "Student");
            var issuer = await app.CreateUserAsync("issuer@example.test", "Instructor");
            var course = new Course { Title = "<script>course</script>", Price = 200000, InstructorId = issuer, IsPublished = true, Status = CourseStatus.Approved };
            db.Add(course); db.Coupons.Add(new() { Code = "DEMO10", InstructorId = issuer, DiscountPercent = 10,
                StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(1) }); await db.SaveChangesAsync();
            rig.CourseId = course.Id; await TestApplication.LoginAsync(browser, "buyer@example.test", "/"); return rig;
        }
        public async Task<string> CreateOrder()
        {
            var html = await Browser.Client.GetStringAsync($"/Checkout/Create?courseId={CourseId}");
            var response = await Browser.Client.PostAsync("/Checkout/Create", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["CourseId"] = CourseId.ToString(), ["SubmissionKey"] = Hidden(html, "SubmissionKey"), ["__RequestVerificationToken"] = Hidden(html, "__RequestVerificationToken") }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); return response.Headers.Location!.ToString();
        }
        public async ValueTask DisposeAsync() { browser.Dispose(); app.Dispose(); scope.Dispose(); await data.DisposeAsync(); }
    }
}
