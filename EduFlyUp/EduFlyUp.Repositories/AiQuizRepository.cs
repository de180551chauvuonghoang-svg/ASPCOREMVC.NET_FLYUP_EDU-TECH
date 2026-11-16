using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;

namespace EduFlyUp.Repositories;

public class AiQuizRepository : IAiQuizRepository
{
    public Task<LessonMaterialContext?> GetLessonMaterialContextAsync(int lessonId, CancellationToken cancellationToken = default)
        => AiQuizDAO.Instance.GetLessonMaterialContextAsync(lessonId, cancellationToken);
}
