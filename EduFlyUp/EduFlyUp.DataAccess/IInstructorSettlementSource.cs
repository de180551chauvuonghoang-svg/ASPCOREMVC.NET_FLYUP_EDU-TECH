using EduFlyUp.BusinessObjects;

namespace EduFlyUp.DataAccess;

/// <summary>
/// The payment module must return a complete snapshot of verified, settled receipts for this
/// instructor. PaidAmount is the actual collected amount after discounts, not the list price;
/// RefundedAmount is the cumulative confirmed refund for that receipt. References must be
/// stable, canonical and unique. Null means unverified/unavailable; an empty list means verified
/// with no receipts. Never return a partial snapshot as complete. Read the supplied context and
/// transaction; balance-affecting writers must use the EduFlyUp.PayoutManagement SQL lock.
/// </summary>
public interface IInstructorSettlementSource
{
    Task<IReadOnlyList<InstructorSettledReceipt>?> ReadVerifiedReceiptsAsync(AppDbContext db, InstructorIdentity instructor);
}

public sealed class UnavailableInstructorSettlementSource : IInstructorSettlementSource
{
    public Task<IReadOnlyList<InstructorSettledReceipt>?> ReadVerifiedReceiptsAsync(AppDbContext db, InstructorIdentity instructor)
        => Task.FromResult<IReadOnlyList<InstructorSettledReceipt>?>(null);
}
