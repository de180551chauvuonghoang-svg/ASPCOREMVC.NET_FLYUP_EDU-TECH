using System.Net;
using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlyUp.Tests;

public sealed class AdminPayoutHttpTests
{
    [SqlServerFact]
    public async Task Only_admin_can_confirm_transfer_and_only_once_from_verified_balance()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync(useMigrations: true);
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var source = new SePayInstructorSettlementSource();
        using var app = new TestApplication(settlements: source, sqlConnection: db.Database.GetConnectionString());
        var instructorId = await app.CreateUserAsync("instructor@example.test", "Instructor");
        var adminId = await app.CreateUserAsync("admin@example.test", "Admin");
        const string account = "000009876543";
        var payable = new PayoutRequest { InstructorId = instructorId, Amount = 125000, AccountNumber = account };
        var excessive = new PayoutRequest { InstructorId = instructorId, Amount = 2000 };
        db.PayoutRequests.AddRange(payable, excessive);
        db.SePayReceipts.Add(new SePayReceipt
        {
            TransactionId = 13, OrderId = 32, InstructorId = instructorId,
            Amount = 180000, VerifiedAtUtc = DateTime.UtcNow, CourseTitle = "Demo"
        });
        await db.SaveChangesAsync();

        using var admin = app.CreateBrowser();
        using var instructor = app.CreateBrowser();
        using var guest = app.CreateBrowser();
        await TestApplication.LoginAsync(admin, "admin@example.test");
        await TestApplication.LoginAsync(instructor, "instructor@example.test", "/");
        var detail = $"/AdminPayouts/Details/{payable.Id}";
        var confirm = $"/AdminPayouts/ConfirmTransfer/{payable.Id}";
        var reject = $"/AdminPayouts/Reject/{excessive.Id}";
        Assert.Equal(HttpStatusCode.Redirect, (await guest.Client.GetAsync(detail)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await instructor.Client.GetAsync(detail)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await instructor.Client.PostAsync(confirm, Form("FAKE"))).StatusCode);
        var listing = await admin.Client.GetAsync("/AdminPayouts");
        Assert.Equal(HttpStatusCode.OK, listing.StatusCode);
        Assert.DoesNotContain(account, await listing.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Client.GetAsync("/AdminPayouts/Details/2147483647")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.Client.GetAsync(confirm)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.PostAsync(confirm, Form("NO TOKEN"))).StatusCode);

        var response = await admin.Client.GetAsync(detail);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains(account, await response.Content.ReadAsStringAsync());
        var token = await TestApplication.AntiForgeryTokenAsync(admin, detail);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.PostAsync(confirm, Form(" ", token))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.Client.PostAsync(confirm, Form("DEMO-1", token))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.Client.PostAsync(reject, Form("<b>Vượt số dư</b>", token))).StatusCode);
        var rejected = await admin.Client.GetStringAsync($"/AdminPayouts/Details/{excessive.Id}");
        Assert.Contains("&lt;b&gt;", rejected);
        Assert.DoesNotContain("<b>Vượt số dư</b>", rejected);

        var decisions = await Task.WhenAll(
            admin.Client.PostAsync(confirm, Form("DEMO-1", token)),
            admin.Client.PostAsync(confirm, Form("DEMO-2", token)));
        Assert.All(decisions, decision => Assert.Equal(HttpStatusCode.Redirect, decision.StatusCode));
        var processed = await db.PayoutRequests.AsNoTracking().SingleAsync(p => p.Id == payable.Id);
        Assert.Equal(PayoutStatus.Approved, processed.Status);
        Assert.NotNull(processed.ProcessedAt);
        Assert.Contains(processed.AdminNote, new[] { "Mã chuyển khoản: DEMO-1", "Mã chuyển khoản: DEMO-2" });
        Assert.DoesNotContain(account, await admin.Client.GetStringAsync(detail));
        var balance = await InstructorPayoutDAO.Instance.BalanceAsync(db, new SettlementInstructorEarningsSource(source),
            new InstructorIdentity(instructorId, "instructor@example.test"));
        Assert.Equal(1000m, balance.Available);

        // A cookie can retain its role claim after removing that role in the database.
        using var identityScope = app.Services.CreateScope();
        var users = identityScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var adminUser = (await users.FindByIdAsync(adminId))!;
        Assert.True((await users.RemoveFromRoleAsync(adminUser, "Admin")).Succeeded);
        Assert.Equal(AdminPayoutChange.Forbidden, await new AdminPayoutDAO(db, new SettlementInstructorEarningsSource(source), TimeProvider.System)
            .ProcessAsync(payable.Id, adminId, false, "Không hợp lệ"));
    }

    private static FormUrlEncodedContent Form(string note, string? token = null)
    {
        var fields = new Dictionary<string, string> { ["note"] = note };
        if (token is not null)
            fields["__RequestVerificationToken"] = token;
        return new(fields);
    }
}
