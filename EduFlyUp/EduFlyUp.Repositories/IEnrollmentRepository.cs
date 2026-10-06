using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Repositories
{
    public interface IEnrollmentRepository
    {
        Task<IEnumerable<Enrollment>> GetByUserIdAsync(string userId);
        Task<bool> IsEnrolledAsync(string userId, int courseId);
        Task<Enrollment> AddAsync(Enrollment enrollment);
    }
}
