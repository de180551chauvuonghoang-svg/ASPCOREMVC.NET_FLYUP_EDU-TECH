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
        Task DeleteLessonAsync(int id);
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

        public async Task DeleteLessonAsync(int id) => await _lessonRepository.DeleteAsync(id);

        public async Task<int> GetTotalLessonsCountAsync() => await _lessonRepository.CountAsync();
    }
}
