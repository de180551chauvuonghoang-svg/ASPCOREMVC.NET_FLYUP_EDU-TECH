using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;

namespace EduFlyUp.Services;

public interface IInstructorStudentService
{
    Task<InstructorEnrollmentSummary> SummaryAsync(InstructorIdentity instructor);
    Task<InstructorStudentExport?> ExportAsync(InstructorIdentity instructor, int courseId, string? search, EnrollmentStatus? status);
    Task<List<InstructorStudentCourse>> CoursesAsync(InstructorIdentity instructor);
    Task<InstructorStudentRoster?> RosterAsync(InstructorIdentity instructor, int courseId, int page, string? search = null, EnrollmentStatus? status = null);
    Task<InstructorStudentDetail?> DetailAsync(InstructorIdentity instructor, int courseId, int enrollmentId, int page);
}

public sealed class InstructorStudentService(IInstructorStudentRepository repository) : IInstructorStudentService
{
    public Task<InstructorEnrollmentSummary> SummaryAsync(InstructorIdentity instructor)
        => Valid(instructor) ? repository.SummaryAsync(instructor) : Task.FromResult(new InstructorEnrollmentSummary(0, 0));
    public Task<InstructorStudentExport?> ExportAsync(InstructorIdentity instructor, int courseId, string? search, EnrollmentStatus? status)
        => Valid(instructor) && courseId > 0 && search?.Length is not > 100 && (!status.HasValue || Enum.IsDefined(status.Value))
            ? repository.ExportAsync(instructor, courseId, search?.Trim(), status) : Task.FromResult<InstructorStudentExport?>(null);
    private static bool Valid(InstructorIdentity instructor) => !string.IsNullOrWhiteSpace(instructor.UserId) && !string.IsNullOrWhiteSpace(instructor.UserName);
    public Task<List<InstructorStudentCourse>> CoursesAsync(InstructorIdentity instructor)
        => Valid(instructor) ? repository.CoursesAsync(instructor) : Task.FromResult(new List<InstructorStudentCourse>());
    public Task<InstructorStudentRoster?> RosterAsync(InstructorIdentity instructor, int courseId, int page, string? search = null, EnrollmentStatus? status = null)
        => Valid(instructor) && courseId > 0 ? repository.RosterAsync(instructor, courseId, Math.Max(1, page), search?.Trim(), status) : Task.FromResult<InstructorStudentRoster?>(null);
    public Task<InstructorStudentDetail?> DetailAsync(InstructorIdentity instructor, int courseId, int enrollmentId, int page)
        => Valid(instructor) && courseId > 0 && enrollmentId > 0 ? repository.DetailAsync(instructor, courseId, enrollmentId, Math.Max(1, page)) : Task.FromResult<InstructorStudentDetail?>(null);
}
