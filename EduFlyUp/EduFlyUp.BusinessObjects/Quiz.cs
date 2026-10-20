namespace EduFlyUp.BusinessObjects;

/// <summary>
/// Đại diện cho Đề thi / Bài kiểm tra trắc nghiệm trong Khóa học (UC-32)
/// </summary>
public class Quiz : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DurationMinutes { get; set; } = 30;
    public int PassingScorePercentage { get; set; } = 70;
    public bool IsCAT { get; set; } = false; // Computer Adaptive Testing mode

    // Foreign Key & Navigation
    public int CourseId { get; set; }
    public virtual Course Course { get; set; } = null!;

    public virtual ICollection<QuizQuestion> QuizQuestions { get; set; } = new List<QuizQuestion>();
    public virtual ICollection<QuizAttempt> Attempts { get; set; } = new List<QuizAttempt>();
}
