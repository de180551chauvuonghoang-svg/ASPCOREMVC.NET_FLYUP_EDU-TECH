namespace EduFlyUp.BusinessObjects;

public sealed record InstructorPayoutRow(int Id, decimal Amount, PayoutStatus Status, DateTime CreatedAt,
    DateTime? ProcessedAt, string? AdminNote);
public sealed record InstructorPayoutDetail(InstructorPayoutRow Request, string BankName, string MaskedAccountNumber, string AccountHolderName);
public sealed record InstructorPayoutHistory(IReadOnlyList<InstructorPayoutRow> Items, int Page, int TotalCount, PayoutStatus? StatusFilter = null)
{
    public const int PageSize = 10;
    public int TotalPages => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
}
