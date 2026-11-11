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

        public async Task<bool> HasCourseEnrollmentsAsync(int courseId)
        {
            using var context = new AppDbContext();
            return await context.Enrollments.AnyAsync(e => e.CourseId == courseId);
        }

        public async Task<bool> HasActiveCourseEnrollmentsAsync(int courseId)
        {
            using var context = new AppDbContext();
            return await context.Enrollments.AnyAsync(e =>
                e.CourseId == courseId &&
                (e.Status == EnrollmentStatus.Active || e.Status == EnrollmentStatus.Completed));
        }

        public async Task<Dictionary<int, int>> GetCourseEnrollmentCountsAsync(IEnumerable<int> courseIds)
        {
            var ids = courseIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, int>();

            using var context = new AppDbContext();
            return await context.Enrollments
                .Where(e => ids.Contains(e.CourseId))
                .GroupBy(e => e.CourseId)
                .Select(g => new { CourseId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CourseId, x => x.Count);
        }
    }
}
