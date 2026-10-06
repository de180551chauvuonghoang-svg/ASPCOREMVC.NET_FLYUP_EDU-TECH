using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.DataAccess
{
    public class LessonDAO : SingletonBase<LessonDAO>
    {
        public async Task<IEnumerable<Lesson>> GetByCourseIdAsync(int courseId)
        {
            using var context = new AppDbContext();
            return await context.Lessons
                .Where(l => l.CourseId == courseId)
                .OrderBy(l => l.Order)
                .ToListAsync();
        }

        public async Task<Lesson?> GetByIdAsync(int id)
        {
            using var context = new AppDbContext();
            return await context.Lessons.FindAsync(id);
        }

        public async Task<Lesson> AddAsync(Lesson lesson)
        {
            using var context = new AppDbContext();
            await context.Lessons.AddAsync(lesson);
            await context.SaveChangesAsync();
            return lesson;
        }

        public async Task UpdateAsync(Lesson lesson)
        {
            using var context = new AppDbContext();
            context.Lessons.Update(lesson);
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var context = new AppDbContext();
            var lesson = await context.Lessons.FindAsync(id);
            if (lesson != null)
            {
                context.Lessons.Remove(lesson);
                await context.SaveChangesAsync();
            }
        }

        public async Task<int> CountAsync()
        {
            using var context = new AppDbContext();
            return await context.Lessons.CountAsync();
        }
    }
}
