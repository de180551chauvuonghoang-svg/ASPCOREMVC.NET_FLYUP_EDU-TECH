using EduFlyUp.BusinessObjects;

namespace EduFlyUp.DataAccess;

/// <summary>
/// Cumulative, reconciled instructor earnings after the 70% share and refunds, before payout
/// deductions. SettlementInstructorEarningsSource applies BR-38.1 to each verified receipt.
/// Null means unverified, not a zero balance.
/// Implementations must read the supplied scoped context/transaction and must coordinate with
/// the EduFlyUp.PayoutManagement SQL lock when changing credited earnings or payout status.
/// Never derive earnings from the current course price or enrollment count.
/// </summary>
public interface IInstructorEarningsSource
{
    Task<decimal?> CreditedEarningsAsync(AppDbContext db, InstructorIdentity instructor);
}

public sealed class SettlementInstructorEarningsSource(IInstructorSettlementSource settlements) : IInstructorEarningsSource
{
    public async Task<decimal?> CreditedEarningsAsync(AppDbContext db, InstructorIdentity instructor)
    {
        if (string.IsNullOrWhiteSpace(instructor.UserId) || string.IsNullOrWhiteSpace(instructor.UserName)) return null;
        var receipts = await settlements.ReadVerifiedReceiptsAsync(db, instructor);
        if (receipts is null) return null;
        var references = new HashSet<string>(StringComparer.Ordinal);
        decimal total = 0;
        foreach (var receipt in receipts)
        {
            if (receipt is null || string.IsNullOrWhiteSpace(receipt.Reference) || !references.Add(receipt.Reference)
                || (receipt.InstructorId != instructor.UserId && !string.Equals(receipt.InstructorId, instructor.UserName, StringComparison.OrdinalIgnoreCase))) return null;
            var share = InstructorRevenueShare.Calculate(receipt.PaidAmount, receipt.RefundedAmount);
            if (share is null) return null;
            try { total += share.InstructorAmount; }
            catch (OverflowException) { return null; }
        }
        return total;
    }
}
