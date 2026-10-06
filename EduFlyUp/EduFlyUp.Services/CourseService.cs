using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;

namespace EduFlyUp.Services
{
    public interface ICourseService
    {
        Task<IEnumerable<Course>> GetAllCoursesAsync();
        Task<IEnumerable<Course>> GetPublishedCoursesAsync();
        Task<Course?> GetCourseByIdAsync(int id);
        Task<Course> CreateCourseAsync(Course course);
        Task UpdateCourseAsync(Course course);
        Task DeleteCourseAsync(int id);
        Task<int> GetTotalCoursesCountAsync();
        Task<int> GetPublishedCoursesCountAsync();
    }

    public class CourseService : ICourseService
    {
        private readonly ICourseRepository _courseRepository;

        public CourseService(ICourseRepository courseRepository)
        {
            _courseRepository = courseRepository;
        }

        public async Task<IEnumerable<Course>> GetAllCoursesAsync() => await _courseRepository.GetAllAsync();

        public async Task<IEnumerable<Course>> GetPublishedCoursesAsync()
        {
            var courses = await _courseRepository.GetAllAsync();
            return courses.Where(c => c.IsPublished);
        }

        public async Task<Course?> GetCourseByIdAsync(int id) => await _courseRepository.GetByIdAsync(id);

        public async Task<Course> CreateCourseAsync(Course course) => await _courseRepository.AddAsync(course);

        public async Task UpdateCourseAsync(Course course) => await _courseRepository.UpdateAsync(course);

        public async Task DeleteCourseAsync(int id) => await _courseRepository.DeleteAsync(id);

        public async Task<int> GetTotalCoursesCountAsync() => await _courseRepository.CountAsync();

        public async Task<int> GetPublishedCoursesCountAsync() => await _courseRepository.CountPublishedAsync();
    }
}
