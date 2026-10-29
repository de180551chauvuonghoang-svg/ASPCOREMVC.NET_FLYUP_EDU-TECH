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

    public Task<IEnumerable<Course>> GetByInstructorIdAsync(string instructorId, CourseStatus? status = null, string? search = null, string? category = null)
    {
        var query = Courses.Where(c => c.InstructorId == instructorId);
        if (status.HasValue) query = query.Where(c => c.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(c => c.Title.Contains(search, StringComparison.OrdinalIgnoreCase) || c.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(c => c.Category == category);
        return Task.FromResult<IEnumerable<Course>>(query.ToList());
    }

    public Task<bool> UpdateStatusAsync(int id, CourseStatus newStatus, string? rejectionReason = null)
    {
        var c = Courses.Find(c => c.Id == id);
        if (c is null) return Task.FromResult(false);
        c.Status = newStatus;
        c.IsPublished = (newStatus == CourseStatus.Approved);
        if (rejectionReason != null) c.RejectionReason = rejectionReason;
        return Task.FromResult(true);
    }

    public Task<IEnumerable<string>> GetCategoriesAsync() =>
        Task.FromResult<IEnumerable<string>>(Courses.Select(c => c.Category).Where(cat => !string.IsNullOrEmpty(cat)).Distinct().ToList());
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
    public List<Lesson> Lessons { get; } = [];
    public bool FailUpdates { get; set; }
    public Task<IEnumerable<Lesson>> GetByCourseIdAsync(int id) => Task.FromResult<IEnumerable<Lesson>>(Lessons.Where(l => l.CourseId == id).ToList());
    public Task<Lesson?> GetByIdAsync(int id) => Task.FromResult(Lessons.Find(l => l.Id == id));
    public Task<Lesson> AddAsync(Lesson lesson) { Lessons.Add(lesson); return Task.FromResult(lesson); }
    public Task UpdateAsync(Lesson lesson) => FailUpdates ? Task.FromException(new InvalidOperationException("Test save failure")) : Task.CompletedTask;
    public Task DeleteAsync(int id) { Lessons.RemoveAll(l => l.Id == id); return Task.CompletedTask; }
    public Task<int> CountAsync() => Task.FromResult(Lessons.Count);
}

internal sealed class TestMaterialStorage : EduFlyUp.Web.Storage.ILessonMaterialStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];
    public async Task<string> SaveAsync(Microsoft.AspNetCore.Http.IFormFile file, CancellationToken cancellationToken)
    {
        var name = Guid.NewGuid().ToString("N") + ".pdf";
        using var content = new MemoryStream();
        await file.CopyToAsync(content, cancellationToken);
        Files[name] = content.ToArray();
        return name;
    }
    public Stream? OpenRead(string fileName) => Files.TryGetValue(fileName, out var bytes) ? new MemoryStream(bytes) : null;
    public void Delete(string fileName) => Files.Remove(fileName);
}
