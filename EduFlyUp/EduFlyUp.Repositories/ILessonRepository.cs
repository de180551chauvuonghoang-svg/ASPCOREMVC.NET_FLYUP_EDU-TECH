using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Repositories
{
    public interface ILessonRepository
    {
        Task<IEnumerable<Lesson>> GetByCourseIdAsync(int courseId);
        Task<Lesson?> GetByIdAsync(int id);
        Task<Lesson> AddAsync(Lesson lesson);
        Task UpdateAsync(Lesson lesson);
        Task<bool> TryUpdateMaterialAsync(int id, string expectedUrl, string materialUrl, string attachmentName);
        Task DeleteAsync(int id);
        Task<int> CountAsync();
    }
}
