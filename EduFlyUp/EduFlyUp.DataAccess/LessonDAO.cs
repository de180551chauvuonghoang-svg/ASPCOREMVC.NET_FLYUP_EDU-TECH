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
            await UpdateContentAsync(context, lesson);
        }

        internal static async Task UpdateContentAsync(AppDbContext context, Lesson lesson)
        {
            // Update content without restoring a stale upload after replacement/removal.
            var preserveMaterial = lesson.MaterialUrl.StartsWith("/LessonMaterials/Download/", StringComparison.Ordinal);
            var query = context.Lessons.Where(l => l.Id == lesson.Id);
            await query.ExecuteUpdateAsync(setters => setters
                .SetProperty(l => l.Title, lesson.Title)
                .SetProperty(l => l.Content, lesson.Content)
                .SetProperty(l => l.VideoUrl, lesson.VideoUrl)
                .SetProperty(l => l.SectionName, lesson.SectionName)
                .SetProperty(l => l.DurationMinutes, lesson.DurationMinutes)
                .SetProperty(l => l.Order, lesson.Order)
                .SetProperty(l => l.IsPreview, lesson.IsPreview)
                .SetProperty(l => l.UpdatedAt, DateTime.UtcNow)
                .SetProperty(l => l.MaterialUrl, l => preserveMaterial || l.MaterialUrl.StartsWith("/LessonMaterials/Download/") ? l.MaterialUrl : lesson.MaterialUrl)
                .SetProperty(l => l.AttachmentName, l => preserveMaterial || l.MaterialUrl.StartsWith("/LessonMaterials/Download/") ? l.AttachmentName : lesson.AttachmentName));
        }

        public async Task<bool> TryUpdateMaterialAsync(int id, string expectedUrl, string materialUrl, string attachmentName)
        {
            using var context = new AppDbContext();
            return await TryUpdateMaterialAsync(context, id, expectedUrl, materialUrl, attachmentName);
        }

        internal static async Task<bool> TryUpdateMaterialAsync(AppDbContext context, int id, string expectedUrl, string materialUrl, string attachmentName)
        {
            return await context.Lessons.Where(l => l.Id == id && l.MaterialUrl == expectedUrl)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.MaterialUrl, materialUrl)
                    .SetProperty(l => l.AttachmentName, attachmentName)
                    .SetProperty(l => l.UpdatedAt, DateTime.UtcNow)) == 1;
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
