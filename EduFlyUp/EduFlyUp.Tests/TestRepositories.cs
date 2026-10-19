using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;

namespace EduFlyUp.Tests;

internal sealed class TestCourseRepository : ICourseRepository
{
    public List<Course> Courses { get; } = [];
    public Task<IEnumerable<Course>> GetAllAsync() => Task.FromResult<IEnumerable<Course>>(Courses);
    public Task<Course?> GetByIdAsync(int id) => Task.FromResult(Courses.Find(c => c.Id == id));
    public Task<Course> AddAsync(Course course)
    {
        course.Id = Courses.Select(c => c.Id).DefaultIfEmpty().Max() + 1;
        Courses.Add(course);
        return Task.FromResult(course);
    }
    public Task UpdateAsync(Course course) => Task.CompletedTask;
    public Task DeleteAsync(int id) { Courses.RemoveAll(c => c.Id == id); return Task.CompletedTask; }
    public Task<int> CountAsync() => Task.FromResult(Courses.Count);
    public Task<int> CountPublishedAsync() => Task.FromResult(Courses.Count(c => c.IsPublished));
}

internal sealed class TestLessonRepository : ILessonRepository
{
    public Task<IEnumerable<Lesson>> GetByCourseIdAsync(int id) => Task.FromResult<IEnumerable<Lesson>>([]);
    public Task<Lesson?> GetByIdAsync(int id) => Task.FromResult<Lesson?>(null);
    public Task<Lesson> AddAsync(Lesson lesson) => Task.FromResult(lesson);
    public Task UpdateAsync(Lesson lesson) => Task.CompletedTask;
    public Task DeleteAsync(int id) => Task.CompletedTask;
    public Task<int> CountAsync() => Task.FromResult(0);
}
