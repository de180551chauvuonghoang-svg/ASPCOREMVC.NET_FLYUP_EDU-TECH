using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;

namespace EduFlyUp.Services
{
    public interface ILessonService
    {
        Task<IEnumerable<Lesson>> GetLessonsByCourseIdAsync(int courseId);
        Task<Lesson?> GetLessonByIdAsync(int id);
        Task<Lesson> CreateLessonAsync(Lesson lesson);
        Task UpdateLessonAsync(Lesson lesson);
        Task<bool> TryUpdateMaterialAsync(int id, string expectedUrl, string materialUrl, string attachmentName);
        Task<bool> TryUpdateVideoAsync(int id, string expectedUrl, string videoUrl);
        Task<Lesson?> DeleteLessonAsync(int id);
        Task<int> GetTotalLessonsCountAsync();
    }

    public class LessonService : ILessonService
    {
        private readonly ILessonRepository _lessonRepository;

        public LessonService(ILessonRepository lessonRepository)
        {
            _lessonRepository = lessonRepository;
        }

        public async Task<IEnumerable<Lesson>> GetLessonsByCourseIdAsync(int courseId) => await _lessonRepository.GetByCourseIdAsync(courseId);

        public async Task<Lesson?> GetLessonByIdAsync(int id) => await _lessonRepository.GetByIdAsync(id);

        public async Task<Lesson> CreateLessonAsync(Lesson lesson) => await _lessonRepository.AddAsync(lesson);

        public async Task UpdateLessonAsync(Lesson lesson) => await _lessonRepository.UpdateAsync(lesson);

        public Task<bool> TryUpdateMaterialAsync(int id, string expectedUrl, string materialUrl, string attachmentName)
            => _lessonRepository.TryUpdateMaterialAsync(id, expectedUrl, materialUrl, attachmentName);

        public Task<bool> TryUpdateVideoAsync(int id, string expectedUrl, string videoUrl)
            => _lessonRepository.TryUpdateVideoAsync(id, expectedUrl, videoUrl);

        public Task<Lesson?> DeleteLessonAsync(int id) => _lessonRepository.DeleteAsync(id);

        public async Task<int> GetTotalLessonsCountAsync() => await _lessonRepository.CountAsync();
    }
}
