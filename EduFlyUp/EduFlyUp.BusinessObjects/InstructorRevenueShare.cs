namespace EduFlyUp.BusinessObjects;

public sealed record InstructorSettledReceipt(string Reference, string InstructorId, decimal PaidAmount, decimal RefundedAmount,
    InstructorSettlementInfo? Info = null);

public sealed record InstructorRevenueShare(decimal NetCollected, decimal InstructorAmount, decimal PlatformAmount)
{
    public const decimal InstructorRate = 0.70m;

    // Apply BR-38.1 per settled receipt, after discounts (already in PaidAmount) and refunds.
    // Round the instructor share once, then assign the remainder to the platform.
    public static InstructorRevenueShare? Calculate(decimal paidAmount, decimal refundedAmount)
    {
        const decimal maxMoney = 9999999999999999.99m;
        if (paidAmount < 0 || paidAmount > maxMoney || refundedAmount < 0 || refundedAmount > paidAmount
            || decimal.Round(paidAmount, 2) != paidAmount || decimal.Round(refundedAmount, 2) != refundedAmount) return null;
        var net = paidAmount - refundedAmount;
        var instructor = decimal.Round(net * InstructorRate, 2, MidpointRounding.AwayFromZero);
        return new(net, instructor, net - instructor);
    }
}
