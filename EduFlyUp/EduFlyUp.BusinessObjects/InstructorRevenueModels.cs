namespace EduFlyUp.BusinessObjects;

// Optional snapshots from the payment/settlement source, never inferred from today's course price.
public sealed record InstructorSettlementInfo(int? CourseId, string? CourseTitle, string? StudentName,
    decimal? OriginalPrice, DateTime? SettledAtUtc);
public sealed record InstructorRevenueItem(InstructorSettledReceipt Receipt, InstructorRevenueShare Share);
public sealed record InstructorRevenueStatement(IReadOnlyList<InstructorRevenueItem> Items, decimal TotalNet,
    decimal TotalInstructor, decimal TotalPlatform);
public sealed record InstructorRevenuePage(IReadOnlyList<InstructorRevenueItem> Items, int Page, int TotalCount,
    decimal TotalNet, decimal TotalInstructor, decimal TotalPlatform, string? Search = null)
{
    public const int PageSize = 10;
    public int TotalPages => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
}
