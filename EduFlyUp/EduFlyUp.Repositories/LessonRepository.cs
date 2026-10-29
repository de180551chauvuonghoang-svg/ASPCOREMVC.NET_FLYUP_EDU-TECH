using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;

namespace EduFlyUp.Repositories
{
    public class LessonRepository : ILessonRepository
    {
        public async Task<IEnumerable<Lesson>> GetByCourseIdAsync(int courseId) => await LessonDAO.Instance.GetByCourseIdAsync(courseId);

        public async Task<Lesson?> GetByIdAsync(int id) => await LessonDAO.Instance.GetByIdAsync(id);

        public async Task<Lesson> AddAsync(Lesson lesson) => await LessonDAO.Instance.AddAsync(lesson);

        public async Task UpdateAsync(Lesson lesson) => await LessonDAO.Instance.UpdateAsync(lesson);

        public Task<bool> TryUpdateMaterialAsync(int id, string expectedUrl, string materialUrl, string attachmentName)
            => LessonDAO.Instance.TryUpdateMaterialAsync(id, expectedUrl, materialUrl, attachmentName);

        public async Task DeleteAsync(int id) => await LessonDAO.Instance.DeleteAsync(id);

        public async Task<int> CountAsync() => await LessonDAO.Instance.CountAsync();
    }
}
