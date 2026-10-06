using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;

namespace EduFlyUp.Repositories
{
    public class CourseRepository : ICourseRepository
    {
        public async Task<IEnumerable<Course>> GetAllAsync() => await CourseDAO.Instance.GetAllAsync();

        public async Task<Course?> GetByIdAsync(int id) => await CourseDAO.Instance.GetByIdAsync(id);

        public async Task<Course> AddAsync(Course course) => await CourseDAO.Instance.AddAsync(course);

        public async Task UpdateAsync(Course course) => await CourseDAO.Instance.UpdateAsync(course);

        public async Task DeleteAsync(int id) => await CourseDAO.Instance.DeleteAsync(id);

        public async Task<int> CountAsync() => await CourseDAO.Instance.CountAsync();

        public async Task<int> CountPublishedAsync() => await CourseDAO.Instance.CountPublishedAsync();
    }
}
