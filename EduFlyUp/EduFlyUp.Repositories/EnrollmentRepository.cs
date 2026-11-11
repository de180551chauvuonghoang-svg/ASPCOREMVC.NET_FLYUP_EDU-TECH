using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;

namespace EduFlyUp.Repositories
{
    public class EnrollmentRepository : IEnrollmentRepository
    {
        public async Task<IEnumerable<Enrollment>> GetByUserIdAsync(string userId) => await EnrollmentDAO.Instance.GetByUserIdAsync(userId);

        public async Task<bool> IsEnrolledAsync(string userId, int courseId) => await EnrollmentDAO.Instance.IsEnrolledAsync(userId, courseId);

        public async Task<Enrollment> AddAsync(Enrollment enrollment) => await EnrollmentDAO.Instance.AddAsync(enrollment);

        public async Task<bool> HasCourseEnrollmentsAsync(int courseId) => await EnrollmentDAO.Instance.HasCourseEnrollmentsAsync(courseId);

        public async Task<bool> HasActiveCourseEnrollmentsAsync(int courseId) => await EnrollmentDAO.Instance.HasActiveCourseEnrollmentsAsync(courseId);

        public async Task<Dictionary<int, int>> GetCourseEnrollmentCountsAsync(IEnumerable<int> courseIds) => await EnrollmentDAO.Instance.GetCourseEnrollmentCountsAsync(courseIds);
    }
}
