namespace EduFlyUp.BusinessObjects;

/// <summary>
/// Đại diện cho một câu hỏi trắc nghiệm do AI sinh ra từ tài liệu
/// </summary>
public class AiGeneratedQuestion
{
    public string Question { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
    public int CorrectAnswerIndex { get; set; }
    public string Explanation { get; set; } = string.Empty;
}

/// <summary>
/// Kết quả phản hồi của quá trình AI sinh Quiz
/// </summary>
public class AiQuizResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string LessonTitle { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public List<AiGeneratedQuestion> Questions { get; set; } = new();
}

/// <summary>
/// Ngữ cảnh học liệu bài giảng truyền từ DataAccess sang Services
/// </summary>
public class LessonMaterialContext
{
    public int LessonId { get; set; }
    public string LessonTitle { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string MaterialUrl { get; set; } = string.Empty;
    public string AttachmentName { get; set; } = string.Empty;
    public string ExtractedDocumentText { get; set; } = string.Empty;
}
