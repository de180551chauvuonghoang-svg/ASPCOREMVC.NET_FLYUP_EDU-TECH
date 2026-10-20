namespace EduFlyUp.BusinessObjects;

/// <summary>
/// Bảng quan hệ nhiều-nhiều giữa Đề thi (Quiz) và Câu hỏi (Question)
/// </summary>
public class QuizQuestion : BaseEntity
{
    public int QuizId { get; set; }
    public virtual Quiz Quiz { get; set; } = null!;

    public int QuestionId { get; set; }
    public virtual Question Question { get; set; } = null!;

    public int Order { get; set; } = 1;
}
