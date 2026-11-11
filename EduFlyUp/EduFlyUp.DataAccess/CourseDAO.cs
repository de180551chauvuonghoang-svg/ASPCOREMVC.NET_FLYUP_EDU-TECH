using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.DataAccess
{
    public class CourseDAO : SingletonBase<CourseDAO>
    {
        public async Task<IEnumerable<Course>> GetAllAsync()
        {
            using var context = new AppDbContext();
            return await context.Courses
                .Include(c => c.Lessons)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<Course?> GetByIdAsync(int id)
        {
            using var context = new AppDbContext();
            return await context.Courses
                .Include(c => c.Lessons.OrderBy(l => l.Order))
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<Course> AddAsync(Course course)
        {
            using var context = new AppDbContext();
            await context.Courses.AddAsync(course);
            await context.SaveChangesAsync();
            return course;
        }

        public Task UpdateAsync(Course course) => UpdateAsync(course, includePublication: true);

        public Task UpdateContentAsync(Course course) => UpdateAsync(course, includePublication: false);

        private async Task UpdateAsync(Course course, bool includePublication)
        {
            using var context = new AppDbContext();
            var stored = await context.Courses.FindAsync(course.Id)
                ?? throw new DbUpdateConcurrencyException("Khóa học không còn tồn tại.");

            stored.Title = course.Title;
            stored.Description = course.Description;
            stored.ThumbnailUrl = course.ThumbnailUrl;
            stored.Price = course.Price;
            stored.Level = course.Level;
            stored.Category = course.Category;
            // Instructor content edits must never write a stale publication flag,
            // even when an Admin hides the course concurrently.
            if (includePublication)
            {
                stored.IsPublished = course.IsPublished;
                if (course.IsPublished)
                {
                    stored.Status = CourseStatus.Approved;
                    stored.RejectionReason = null;
                }
                else if (stored.Status is CourseStatus.Approved or CourseStatus.PendingApproval)
                {
                    stored.Status = CourseStatus.Rejected;
                }
            }
            await context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<Lesson>> DeleteAsync(int id)
        {
            using var context = new AppDbContext();
            return await DeleteAsync(context, id);
        }

        internal static async Task<IReadOnlyList<Lesson>> DeleteAsync(AppDbContext context, int id)
        {
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var course = await context.Courses.Include(c => c.Lessons).FirstOrDefaultAsync(c => c.Id == id);
            var lessons = course?.Lessons.ToList() ?? [];
            if (course != null)
            {
                if (course.IsPublished)
                {
                    throw new InvalidOperationException("Không thể xóa khóa học đang công khai mở bán. Vui lòng lưu trữ hoặc ẩn khóa học trước khi xóa.");
                }

                if (await context.Enrollments.AnyAsync(e => e.CourseId == id))
                {
                    throw new InvalidOperationException("Không thể xóa khóa học đã có học viên ghi danh. Vui lòng sử dụng tính năng Lưu trữ để bảo toàn quyền học cho học viên cũ.");
                }

                var courseCoupons = await context.Coupons.Where(cp => cp.CourseId == id).ToListAsync();
                if (courseCoupons.Any(cp => cp.UsedCount > 0))
                {
                    throw new InvalidOperationException("Không thể xóa khóa học do đã có mã giảm giá được sử dụng trong đơn hàng.");
                }
                if (courseCoupons.Count > 0)
                {
                    context.Coupons.RemoveRange(courseCoupons);
                }

                var quizQuestions = await context.QuizQuestions
                    .Where(qq => qq.Quiz.CourseId == id || qq.Question.CourseId == id)
                    .ToListAsync();
                if (quizQuestions.Count > 0)
                {
                    context.QuizQuestions.RemoveRange(quizQuestions);
                }

                context.Courses.Remove(course);
                await context.SaveChangesAsync();
            }
            await transaction.CommitAsync();
            return lessons;
        }

        public async Task<int> CountAsync()
        {
            using var context = new AppDbContext();
            return await context.Courses.CountAsync();
        }

        public async Task<int> CountPublishedAsync()
        {
            using var context = new AppDbContext();
            return await context.Courses.CountAsync(c => c.IsPublished);
        }

        public async Task<IEnumerable<Course>> GetByInstructorIdAsync(
            string instructorId, 
            CourseStatus? status = null, 
            string? search = null, 
            string? category = null)
        {
            using var context = new AppDbContext();
            var query = context.Courses
                .Include(c => c.Lessons)
                .Include(c => c.Quizzes)
                .Include(c => c.Questions)
                .Where(c => c.InstructorId == instructorId);

            if (status.HasValue)
            {
                query = query.Where(c => c.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();
                query = query.Where(c => c.Title.Contains(keyword) || c.Description.Contains(keyword));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(c => c.Category == category);
            }

            return await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
        }

        public async Task<bool> UpdateStatusAsync(int id, CourseStatus newStatus, string? rejectionReason = null)
        {
            using var context = new AppDbContext();
            var course = await context.Courses.FindAsync(id);
            if (course == null) return false;

            course.Status = newStatus;
            course.IsPublished = (newStatus == CourseStatus.Approved);

            if (rejectionReason != null)
            {
                course.RejectionReason = rejectionReason;
            }

            await context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<string>> GetCategoriesAsync()
        {
            using var context = new AppDbContext();
            return await context.Courses
                .Where(c => !string.IsNullOrEmpty(c.Category))
                .Select(c => c.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }
    }
}
