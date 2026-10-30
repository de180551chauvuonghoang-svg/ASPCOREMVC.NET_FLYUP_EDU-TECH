using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using EduFlyUp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlyUp.Tests;

public sealed class InstructorPayoutCreationTests
{
    private static readonly InstructorIdentity Owner = new("owner-id", "owner@example.test");
    private static readonly InstructorPayoutInput Input = new(100000, "Demo Bank", "0012345678", "DUONG THAI DUY");
    private sealed class VerifiedTestEarnings(decimal? value) : IInstructorEarningsSource
    {
        public Task<decimal?> CreditedEarningsAsync(AppDbContext db, InstructorIdentity instructor)
            => Task.FromResult(instructor == Owner ? value : null);
    }

    [SqlServerFact]
    public async Task Creation_requires_verified_balance_and_never_writes_when_source_is_unavailable()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = new InstructorPayoutService(new InstructorPayoutRepository(db));
        Assert.Null((await service.BalanceAsync(Owner)).Available);
        Assert.Equal(InstructorPayoutChange.EarningsUnavailable, (await service.CreateAsync(Owner, Input)).Change);
        Assert.Equal(InstructorPayoutChange.Invalid, (await service.CreateAsync(Owner, Input with { Amount = -1 })).Change);
        Assert.Equal(InstructorPayoutChange.Invalid, (await service.CreateAsync(new("", ""), Input)).Change);
        Assert.Empty(await db.PayoutRequests.ToListAsync());
    }

    [SqlServerFact]
    public async Task Creation_checks_deductions_legacy_ownership_and_persists_only_pending_server_owned_fields()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.PayoutRequests.AddRange(new PayoutRequest { InstructorId = Owner.UserName, Amount = 200000, Status = PayoutStatus.Approved },
            new PayoutRequest { InstructorId = Owner.UserId, Amount = 900000, Status = PayoutStatus.Rejected },
            new PayoutRequest { InstructorId = "foreign", Amount = 999999, Status = PayoutStatus.Pending });
        await db.SaveChangesAsync();
        var service = new InstructorPayoutService(new InstructorPayoutRepository(db, new VerifiedTestEarnings(700000)));
        Assert.Equal(500000m, (await service.BalanceAsync(Owner)).Available);
        Assert.Equal(InstructorPayoutChange.InsufficientBalance, (await service.CreateAsync(Owner, Input with { Amount = 500000.01m })).Change);
        Assert.Equal(InstructorPayoutChange.Created, (await service.CreateAsync(Owner, Input with { BankName = " Demo Bank " })).Change);
        var saved = await db.PayoutRequests.AsNoTracking().SingleAsync(p => p.InstructorId == Owner.UserId && p.Status == PayoutStatus.Pending);
        Assert.Equal(Input.Amount, saved.Amount); Assert.Equal(Input.AccountNumber, saved.AccountNumber); Assert.Equal("Demo Bank", saved.BankName);
        Assert.Null(saved.AdminNote); Assert.Null(saved.ProcessedAt);
        Assert.Equal(400000m, (await service.BalanceAsync(Owner)).Available);
        Assert.Equal(InstructorPayoutChange.PendingExists, (await service.CreateAsync(Owner, Input)).Change);
        Assert.Equal(4, await db.PayoutRequests.CountAsync());
    }

    [SqlServerFact]
    public async Task Concurrent_requests_in_separate_contexts_create_only_one_pending_request()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        async Task<InstructorPayoutChange> Send()
        {
            using var scope = database.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return (await new InstructorPayoutService(new InstructorPayoutRepository(db, new VerifiedTestEarnings(700000))).CreateAsync(Owner, Input)).Change;
        }
        var changes = await Task.WhenAll(Send(), Send());
        Assert.Single(changes.Where(c => c == InstructorPayoutChange.Created));
        Assert.Single(changes.Where(c => c == InstructorPayoutChange.PendingExists));
        using var verify = database.Services.CreateScope();
        Assert.Equal(1, await verify.ServiceProvider.GetRequiredService<AppDbContext>().PayoutRequests.CountAsync());
    }
}
