using EduFlyUp.BusinessObjects;

namespace EduFlyUp.DataAccess;

/// <summary>
/// Integration point for cumulative, reconciled instructor earnings (70% share already applied,
/// after discounts/refunds, before payout deductions). Null means unverified, not a zero balance.
/// Implementations must read the supplied scoped context/transaction and must coordinate with
/// the EduFlyUp.PayoutManagement SQL lock when changing credited earnings or payout status.
/// Never derive earnings from the current course price or enrollment count.
/// </summary>
public interface IInstructorEarningsSource
{
    Task<decimal?> CreditedEarningsAsync(AppDbContext db, InstructorIdentity instructor);
}

// Replace this registration only when an authoritative settlement source is integrated.
public sealed class UnavailableInstructorEarningsSource : IInstructorEarningsSource
{
    public Task<decimal?> CreditedEarningsAsync(AppDbContext db, InstructorIdentity instructor) => Task.FromResult<decimal?>(null);
}
