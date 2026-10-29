using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;

namespace EduFlyUp.Services;

public interface IInstructorStudentService
{
    Task<List<InstructorStudentCourse>> CoursesAsync(InstructorIdentity instructor);
    Task<InstructorStudentRoster?> RosterAsync(InstructorIdentity instructor, int courseId, int page, string? search = null, EnrollmentStatus? status = null);
}

public sealed class InstructorStudentService(IInstructorStudentRepository repository) : IInstructorStudentService
{
    private static bool Valid(InstructorIdentity instructor) => !string.IsNullOrWhiteSpace(instructor.UserId) && !string.IsNullOrWhiteSpace(instructor.UserName);
    public Task<List<InstructorStudentCourse>> CoursesAsync(InstructorIdentity instructor)
        => Valid(instructor) ? repository.CoursesAsync(instructor) : Task.FromResult(new List<InstructorStudentCourse>());
    public Task<InstructorStudentRoster?> RosterAsync(InstructorIdentity instructor, int courseId, int page, string? search = null, EnrollmentStatus? status = null)
        => Valid(instructor) && courseId > 0 ? repository.RosterAsync(instructor, courseId, Math.Max(1, page), search?.Trim(), status) : Task.FromResult<InstructorStudentRoster?>(null);
}
