using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Repositories
{
    public interface ILessonRepository
    {
        Task<IEnumerable<Lesson>> GetByCourseIdAsync(int courseId);
        Task<Lesson?> GetByIdAsync(int id);
        Task<Lesson> AddAsync(Lesson lesson);
        Task UpdateAsync(Lesson lesson);
        Task DeleteAsync(int id);
        Task<int> CountAsync();
    }
}
