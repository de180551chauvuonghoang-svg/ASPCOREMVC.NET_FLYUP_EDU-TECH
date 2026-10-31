using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;

namespace EduFlyUp.Tests;

public sealed class InstructorSettlementEarningsTests
{
    private static readonly InstructorIdentity Owner = new("owner-id", "owner@example.test");

    [Fact]
    public async Task Unverified_income_stays_unknown_while_verified_empty_is_zero()
    {
        using var db = new AppDbContext();
        Assert.Null(await new SettlementInstructorEarningsSource(new UnavailableInstructorSettlementSource()).CreditedEarningsAsync(db, Owner));
        Assert.Equal(0m, await new SettlementInstructorEarningsSource(new TestSettlements([])).CreditedEarningsAsync(db, Owner));
    }

    [Fact]
    public async Task Shares_are_rounded_per_receipt_and_accept_only_the_instructors_id_or_legacy_username()
    {
        using var db = new AppDbContext();
        var source = new SettlementInstructorEarningsSource(new TestSettlements([
            new("receipt-1", Owner.UserId, 0.05m, 0), new("receipt-2", Owner.UserName, 0.05m, 0),
            new("receipt-refunded", Owner.UserId, 1000000, 1000000)]));
        Assert.Equal(0.08m, await source.CreditedEarningsAsync(db, Owner));
        Assert.Null(await source.CreditedEarningsAsync(db, new("", Owner.UserName)));
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("duplicate")]
    [InlineData("invalid-money")]
    [InlineData("empty-reference")]
    public async Task Inconsistent_snapshots_do_not_produce_an_available_balance(string problem)
    {
        var valid = new InstructorSettledReceipt("receipt-1", Owner.UserId, 1000000, 0);
        var invalid = problem switch
        {
            "foreign" => valid with { Reference = "receipt-2", InstructorId = "other" },
            "duplicate" => valid,
            "invalid-money" => valid with { Reference = "receipt-2", RefundedAmount = 1000001 },
            _ => valid with { Reference = "" }
        };
        using var db = new AppDbContext();
        Assert.Null(await new SettlementInstructorEarningsSource(new TestSettlements([valid, invalid])).CreditedEarningsAsync(db, Owner));
    }

    internal sealed class TestSettlements(IReadOnlyList<InstructorSettledReceipt>? receipts) : IInstructorSettlementSource
    {
        public Task<IReadOnlyList<InstructorSettledReceipt>?> ReadVerifiedReceiptsAsync(AppDbContext db, InstructorIdentity instructor)
            => Task.FromResult(receipts);
    }
}
