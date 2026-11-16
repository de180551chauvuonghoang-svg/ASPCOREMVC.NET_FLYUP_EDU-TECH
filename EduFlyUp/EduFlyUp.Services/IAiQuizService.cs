using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Services;

public interface IAiQuizService
{
    Task<AiQuizResult> GenerateQuizFromLessonMaterialAsync(int lessonId, CancellationToken cancellationToken = default);
    Task<AiQuizResult> GenerateQuizFromCustomTextAsync(string content, string title, CancellationToken cancellationToken = default);
}
