namespace EduFlyUp.Services;

public class AiGeneratedQuestion
{
    public string Question { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
    public int CorrectAnswerIndex { get; set; }
    public string Explanation { get; set; } = string.Empty;
}

public class AiQuizResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string LessonTitle { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public List<AiGeneratedQuestion> Questions { get; set; } = new();
}

public interface IAiQuizService
{
    Task<AiQuizResult> GenerateQuizFromLessonMaterialAsync(int lessonId, CancellationToken cancellationToken = default);
    Task<AiQuizResult> GenerateQuizFromCustomTextAsync(string content, string title, CancellationToken cancellationToken = default);
}
