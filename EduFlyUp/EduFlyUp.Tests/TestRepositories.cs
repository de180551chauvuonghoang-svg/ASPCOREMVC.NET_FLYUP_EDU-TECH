using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;

namespace EduFlyUp.Tests;

internal sealed class TestCourseRepository : ICourseRepository
{
    public List<Course> Courses { get; } = [];
    public int PublicationUpdates { get; private set; }
    public int ContentUpdates { get; private set; }
    public Action? BeforeContentUpdate { get; set; }
    public Task<IEnumerable<Course>> GetAllAsync() => Task.FromResult<IEnumerable<Course>>(Courses);
    public Task<Course?> GetByIdAsync(int id)
    {
        var c = Courses.Find(c => c.Id == id);
        return Task.FromResult(c is null ? null : new Course
        {
            Id = c.Id, Title = c.Title, Description = c.Description, Price = c.Price,
            Level = c.Level, ThumbnailUrl = c.ThumbnailUrl, InstructorId = c.InstructorId,
            IsPublished = c.IsPublished, CreatedAt = c.CreatedAt, Lessons = c.Lessons.ToList()
        });
    }
    public Task<Course> AddAsync(Course course)
    {
        course.Id = Courses.Select(c => c.Id).DefaultIfEmpty().Max() + 1;
        Courses.Add(course);
        return Task.FromResult(course);
    }
    public Task UpdateAsync(Course course)
    {
        PublicationUpdates++;
        CopyContent(course);
        Courses.Single(c => c.Id == course.Id).IsPublished = course.IsPublished;
        return Task.CompletedTask;
    }
    public Task UpdateContentAsync(Course course)
    {
        ContentUpdates++;
        BeforeContentUpdate?.Invoke();
        CopyContent(course);
        return Task.CompletedTask;
    }
    private void CopyContent(Course course)
    {
        var stored = Courses.Single(c => c.Id == course.Id);
        stored.Title = course.Title;
        stored.Description = course.Description;
        stored.Price = course.Price;
        stored.Level = course.Level;
        stored.ThumbnailUrl = course.ThumbnailUrl;
    }
    public Task DeleteAsync(int id) { Courses.RemoveAll(c => c.Id == id); return Task.CompletedTask; }
    public Task<int> CountAsync() => Task.FromResult(Courses.Count);
    public Task<int> CountPublishedAsync() => Task.FromResult(Courses.Count(c => c.IsPublished));
}

internal sealed class TestEnrollmentRepository : IEnrollmentRepository
{
    public List<Enrollment> Enrollments { get; } = [];
    public int Queries { get; private set; }
    public Task<IEnumerable<Enrollment>> GetByUserIdAsync(string userId)
    {
        Queries++;
        return Task.FromResult<IEnumerable<Enrollment>>(Enrollments.Where(e => e.UserId == userId));
    }
    public Task<bool> IsEnrolledAsync(string userId, int courseId)
        => Task.FromResult(Enrollments.Any(e => e.UserId == userId && e.CourseId == courseId));
    public Task<Enrollment> AddAsync(Enrollment enrollment)
    {
        Enrollments.Add(enrollment);
        return Task.FromResult(enrollment);
    }
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
