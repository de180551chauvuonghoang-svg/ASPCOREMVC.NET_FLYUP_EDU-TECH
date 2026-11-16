namespace EduFlyUp.BusinessObjects;

public sealed class SePayReceipt
{
    public long TransactionId { get; set; }
    public int OrderId { get; set; }
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = "";
    public string InstructorId { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime VerifiedAtUtc { get; set; }
}
