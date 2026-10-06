using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.DataAccess
{
    public class EnrollmentDAO : SingletonBase<EnrollmentDAO>
    {
        public async Task<IEnumerable<Enrollment>> GetByUserIdAsync(string userId)
        {
            using var context = new AppDbContext();
            return await context.Enrollments
                .Include(e => e.Course)
                .Where(e => e.UserId == userId)
                .ToListAsync();
        }

        public async Task<bool> IsEnrolledAsync(string userId, int courseId)
        {
            using var context = new AppDbContext();
            return await context.Enrollments.AnyAsync(e => e.UserId == userId && e.CourseId == courseId);
        }

        public async Task<Enrollment> AddAsync(Enrollment enrollment)
        {
            using var context = new AppDbContext();
            await context.Enrollments.AddAsync(enrollment);
            await context.SaveChangesAsync();
            return enrollment;
        }
    }
}
