namespace EduFlyUp.BusinessObjects;

/// <summary>
/// Các lựa chọn đáp án cho câu hỏi trắc nghiệm
/// </summary>
public class QuestionOption : BaseEntity
{
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; } = false;

    // Foreign Key & Navigation
    public int QuestionId { get; set; }
    public virtual Question Question { get; set; } = null!;
}
