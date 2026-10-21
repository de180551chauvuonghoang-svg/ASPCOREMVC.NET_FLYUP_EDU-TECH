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
        Task UpdateCourseContentAsync(Course course);
        Task DeleteCourseAsync(int id);
        Task<int> GetTotalCoursesCountAsync();
        Task<int> GetPublishedCoursesCountAsync();
        Task<IEnumerable<Course>> GetCoursesByInstructorIdAsync(string instructorId, CourseStatus? status = null, string? search = null, string? category = null);
        Task<(bool Success, string Message)> SubmitCourseForApprovalAsync(int courseId, string instructorId);
        Task<(bool Success, string Message)> ArchiveCourseAsync(int courseId, string instructorId);
        Task<IEnumerable<string>> GetCategoriesAsync();
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
        public async Task UpdateCourseContentAsync(Course course) => await _courseRepository.UpdateContentAsync(course);

        public async Task DeleteCourseAsync(int id) => await _courseRepository.DeleteAsync(id);

        public async Task<int> GetTotalCoursesCountAsync() => await _courseRepository.CountAsync();

        public async Task<int> GetPublishedCoursesCountAsync() => await _courseRepository.CountPublishedAsync();

        public async Task<IEnumerable<Course>> GetCoursesByInstructorIdAsync(
            string instructorId, 
            CourseStatus? status = null, 
            string? search = null, 
            string? category = null) =>
            await _courseRepository.GetByInstructorIdAsync(instructorId, status, search, category);

        public async Task<(bool Success, string Message)> SubmitCourseForApprovalAsync(int courseId, string instructorId)
        {
            var course = await _courseRepository.GetByIdAsync(courseId);
            if (course == null)
            {
                return (false, "Không tìm thấy khóa học.");
            }

            if (course.InstructorId != instructorId)
            {
                return (false, "Bạn không có quyền gửi duyệt khóa học này.");
            }

            if (course.Status == CourseStatus.PendingApproval)
            {
                return (false, "Khóa học này đang trong trạng thái chờ phê duyệt.");
            }

            if (course.Lessons == null || !course.Lessons.Any())
            {
                return (false, "Khóa học phải có ít nhất 1 bài giảng trước khi gửi phê duyệt.");
            }

            // Xóa lý do từ chối cũ (nếu có từ lần duyệt trước) khi gửi phê duyệt lại
            await _courseRepository.UpdateStatusAsync(courseId, CourseStatus.PendingApproval, string.Empty);
            return (true, "Đã gửi yêu cầu phê duyệt khóa học thành công đến Admin.");
        }

        public async Task<(bool Success, string Message)> ArchiveCourseAsync(int courseId, string instructorId)
        {
            var course = await _courseRepository.GetByIdAsync(courseId);
            if (course == null)
            {
                return (false, "Không tìm thấy khóa học.");
            }

            if (course.InstructorId != instructorId)
            {
                return (false, "Bạn không có quyền thao tác trên khóa học này.");
            }

            if (course.Status == CourseStatus.Archived)
            {
                return (false, "Khóa học này đã được lưu trữ trước đó.");
            }

            await _courseRepository.UpdateStatusAsync(courseId, CourseStatus.Archived);
            return (true, "Đã lưu trữ / tạm ẩn khóa học thành công.");
        }

        public async Task<IEnumerable<string>> GetCategoriesAsync() =>
            await _courseRepository.GetCategoriesAsync();
    }
}
