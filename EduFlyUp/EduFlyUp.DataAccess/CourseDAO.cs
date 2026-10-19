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
            // Instructor content edits must never write a stale publication flag,
            // even when an Admin hides the course concurrently.
            if (includePublication) stored.IsPublished = course.IsPublished;
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var context = new AppDbContext();
            var course = await context.Courses.FindAsync(id);
            if (course != null)
            {
                context.Courses.Remove(course);
                await context.SaveChangesAsync();
            }
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
    }
}
