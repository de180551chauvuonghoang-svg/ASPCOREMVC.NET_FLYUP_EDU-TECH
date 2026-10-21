using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Repositories
{
    public interface ICourseRepository
    {
        Task<IEnumerable<Course>> GetAllAsync();
        Task<Course?> GetByIdAsync(int id);
        Task<Course> AddAsync(Course course);
        Task UpdateAsync(Course course);
        Task UpdateContentAsync(Course course);
        Task DeleteAsync(int id);
        Task<int> CountAsync();
        Task<int> CountPublishedAsync();
        Task<IEnumerable<Course>> GetByInstructorIdAsync(string instructorId, CourseStatus? status = null, string? search = null, string? category = null);
        Task<bool> UpdateStatusAsync(int id, CourseStatus newStatus, string? rejectionReason = null);
        Task<IEnumerable<string>> GetCategoriesAsync();
    }
}
