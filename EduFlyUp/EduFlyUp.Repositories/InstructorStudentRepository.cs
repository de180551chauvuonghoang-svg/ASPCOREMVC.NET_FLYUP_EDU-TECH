using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;

namespace EduFlyUp.Repositories;

public interface IInstructorStudentRepository
{
    Task<List<InstructorPopularCourse>> PopularCoursesAsync(InstructorIdentity instructor);
    Task<InstructorEnrollmentSummary> SummaryAsync(InstructorIdentity instructor);
    Task<InstructorStudentExport?> ExportAsync(InstructorIdentity instructor, int courseId, string? search, EnrollmentStatus? status);
    Task<List<InstructorStudentCourse>> CoursesAsync(InstructorIdentity instructor);
    Task<InstructorStudentRoster?> RosterAsync(InstructorIdentity instructor, int courseId, int page, string? search = null, EnrollmentStatus? status = null);
    Task<InstructorStudentDetail?> DetailAsync(InstructorIdentity instructor, int courseId, int enrollmentId, int page);
}

public sealed class InstructorStudentRepository(AppDbContext db) : IInstructorStudentRepository
{
    public Task<List<InstructorPopularCourse>> PopularCoursesAsync(InstructorIdentity instructor)
        => InstructorStudentDAO.Instance.PopularCoursesAsync(db, instructor);
    public Task<InstructorEnrollmentSummary> SummaryAsync(InstructorIdentity instructor)
        => InstructorStudentDAO.Instance.SummaryAsync(db, instructor);
    public Task<InstructorStudentExport?> ExportAsync(InstructorIdentity instructor, int courseId, string? search, EnrollmentStatus? status)
        => InstructorStudentDAO.Instance.ExportAsync(db, instructor, courseId, search, status);
    public Task<List<InstructorStudentCourse>> CoursesAsync(InstructorIdentity instructor) => InstructorStudentDAO.Instance.CoursesAsync(db, instructor);
    public Task<InstructorStudentRoster?> RosterAsync(InstructorIdentity instructor, int courseId, int page, string? search = null, EnrollmentStatus? status = null)
        => InstructorStudentDAO.Instance.RosterAsync(db, instructor, courseId, page, search, status);
    public Task<InstructorStudentDetail?> DetailAsync(InstructorIdentity instructor, int courseId, int enrollmentId, int page)
        => InstructorStudentDAO.Instance.DetailAsync(db, instructor, courseId, enrollmentId, page);
}
