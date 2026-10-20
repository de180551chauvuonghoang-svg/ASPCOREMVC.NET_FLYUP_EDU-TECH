namespace EduFlyUp.BusinessObjects;

/// <summary>
/// Đại diện cho câu hỏi trong Ngân hàng câu hỏi (UC-31)
/// </summary>
public class Question : BaseEntity
{
    public string Content { get; set; } = string.Empty;
    public QuestionDifficulty Difficulty { get; set; } = QuestionDifficulty.Medium;
    public string Explanation { get; set; } = string.Empty;
    public int Points { get; set; } = 1;

    // Foreign Key & Navigation
    public int CourseId { get; set; }
    public virtual Course Course { get; set; } = null!;

    public virtual ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
    public virtual ICollection<QuizQuestion> QuizQuestions { get; set; } = new List<QuizQuestion>();
}
