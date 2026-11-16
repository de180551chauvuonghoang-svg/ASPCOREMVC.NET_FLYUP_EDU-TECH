using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Repositories;

public interface IAiQuizRepository
{
    Task<LessonMaterialContext?> GetLessonMaterialContextAsync(int lessonId, CancellationToken cancellationToken = default);
}
