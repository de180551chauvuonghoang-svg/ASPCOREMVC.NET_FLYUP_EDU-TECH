using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Repositories
{
    public interface ICourseRepository
    {
        Task<IEnumerable<Course>> GetAllAsync();
        Task<Course?> GetByIdAsync(int id);
        Task<Course> AddAsync(Course course);
        Task UpdateAsync(Course course);
        Task DeleteAsync(int id);
        Task<int> CountAsync();
        Task<int> CountPublishedAsync();
    }
}
