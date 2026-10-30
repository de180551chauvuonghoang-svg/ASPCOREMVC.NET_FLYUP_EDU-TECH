using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using EduFlyUp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlyUp.Tests;

public sealed class InstructorPayoutDetailTests
{
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
