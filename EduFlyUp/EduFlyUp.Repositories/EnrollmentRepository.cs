using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;

namespace EduFlyUp.Repositories
{
    public class EnrollmentRepository : IEnrollmentRepository
    {
        public async Task<IEnumerable<Enrollment>> GetByUserIdAsync(string userId) => await EnrollmentDAO.Instance.GetByUserIdAsync(userId);

        public async Task<bool> IsEnrolledAsync(string userId, int courseId) => await EnrollmentDAO.Instance.IsEnrolledAsync(userId, courseId);

        public async Task<Enrollment> AddAsync(Enrollment enrollment) => await EnrollmentDAO.Instance.AddAsync(enrollment);
    }
}
