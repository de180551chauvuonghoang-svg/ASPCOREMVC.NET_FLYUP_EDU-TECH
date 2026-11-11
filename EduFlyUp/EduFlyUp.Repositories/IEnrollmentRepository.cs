using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Repositories
{
    public interface IEnrollmentRepository
    {
        Task<IEnumerable<Enrollment>> GetByUserIdAsync(string userId);
        Task<bool> IsEnrolledAsync(string userId, int courseId);
        Task<Enrollment> AddAsync(Enrollment enrollment);
        Task<bool> HasCourseEnrollmentsAsync(int courseId);
        Task<bool> HasActiveCourseEnrollmentsAsync(int courseId);
        Task<Dictionary<int, int>> GetCourseEnrollmentCountsAsync(IEnumerable<int> courseIds);
    }
}
