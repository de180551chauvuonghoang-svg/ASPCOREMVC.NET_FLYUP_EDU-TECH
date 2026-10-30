namespace EduFlyUp.BusinessObjects;

/// <summary>
/// Đại diện cho Yêu cầu rút tiền Payout của Giảng viên (UC-38)
/// </summary>
public class PayoutRequest : BaseEntity
{
    public string InstructorId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public PayoutStatus Status { get; set; } = PayoutStatus.Pending;
    public string? AdminNote { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public Guid? SubmissionKey { get; set; }
}
