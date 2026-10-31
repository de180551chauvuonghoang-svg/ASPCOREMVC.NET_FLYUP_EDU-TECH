using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using EduFlyUp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlyUp.Tests;

public sealed class InstructorSettlementPayoutTests
{
    [SqlServerFact]
    public async Task Verified_receipts_flow_through_revenue_sharing_and_sql_payout_guards_in_the_same_context()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actor = new InstructorIdentity("owner-id", "owner@example.test");
        db.PayoutRequests.AddRange(
            new PayoutRequest { InstructorId = actor.UserName, Amount = 200000, Status = PayoutStatus.Approved },
            new PayoutRequest { InstructorId = actor.UserId, Amount = 900000, Status = PayoutStatus.Rejected },
            new PayoutRequest { InstructorId = "foreign", Amount = 100000, Status = PayoutStatus.Pending });
        await db.SaveChangesAsync();
        var source = new ScopedTestSettlements(db, actor);
        var earnings = new SettlementInstructorEarningsSource(source);
        var service = new InstructorPayoutService(new InstructorPayoutRepository(db, earnings));
        var balance = await service.BalanceAsync(actor);
        Assert.Equal(910000m, balance.CreditedEarnings); Assert.Equal(710000m, balance.Available);
        Assert.Equal(200000m, balance.ApprovedAmount); Assert.Equal(0m, balance.PendingAmount);
        var input = new InstructorPayoutInput(700000, "Demo Bank", "0012345678", "Demo Holder", Guid.NewGuid());
        Assert.Equal(InstructorPayoutChange.InsufficientBalance,
            (await service.CreateAsync(actor, input with { Amount = 710000.01m })).Change);
        Assert.Equal(InstructorPayoutChange.Created, (await service.CreateAsync(actor, input)).Change);
        var after = await service.BalanceAsync(actor);
        Assert.Equal(700000m, after.PendingAmount); Assert.Equal(10000m, after.Available);
        Assert.False(after.CanRequest); Assert.True(source.ReadInsidePayoutTransaction);
        var pending = await db.PayoutRequests.AsNoTracking().SingleAsync(p => p.InstructorId == actor.UserId && p.Status == PayoutStatus.Pending);
        Assert.Equal(input.SubmissionKey, pending.SubmissionKey); Assert.Equal(input.Amount, pending.Amount);
        Assert.Equal(InstructorPayoutChange.AlreadySubmitted, (await service.CreateAsync(actor, input)).Change);
        Assert.Equal(4, await db.PayoutRequests.CountAsync());
    }

    private sealed class ScopedTestSettlements(AppDbContext expectedContext, InstructorIdentity expectedActor) : IInstructorSettlementSource
    {
        public bool ReadInsidePayoutTransaction { get; private set; }
        public Task<IReadOnlyList<InstructorSettledReceipt>?> ReadVerifiedReceiptsAsync(AppDbContext db, InstructorIdentity instructor)
        {
            Assert.Same(expectedContext, db); Assert.Equal(expectedActor, instructor);
            ReadInsidePayoutTransaction |= db.Database.CurrentTransaction is not null;
            IReadOnlyList<InstructorSettledReceipt> rows = [
                new("payment-1", instructor.UserId, 1000000, 200000),
                new("payment-2", instructor.UserName, 500000, 0),
                new("payment-3", instructor.UserId, 3000000, 3000000)];
            return Task.FromResult<IReadOnlyList<InstructorSettledReceipt>?>(rows);
        }
    }
}
