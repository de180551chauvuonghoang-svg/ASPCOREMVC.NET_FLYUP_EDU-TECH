using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using EduFlyUp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlyUp.Tests;

public sealed class InstructorPayoutTests
{
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
            CreatedAt = at, Status = PayoutStatus.Pending, AccountNumber = "PRIVATE_ACCOUNT", AccountHolderName = "PRIVATE_HOLDER"
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
        Assert.Empty((await service.HistoryAsync(new("empty", "empty@example.test"), 1)).Items);
        Assert.Empty((await service.HistoryAsync(new("", "owner@example.test"), 1)).Items);
    }
}
