namespace EduFlyUp.BusinessObjects;

/// <summary>
/// Lưu trữ lịch sử và kết quả học viên làm bài thi trắc nghiệm (UC-35)
/// </summary>
public class QuizAttempt : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public bool Passed { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Foreign Key & Navigation
    public int QuizId { get; set; }
    public virtual Quiz Quiz { get; set; } = null!;
}
