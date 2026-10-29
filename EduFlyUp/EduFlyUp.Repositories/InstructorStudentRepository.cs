using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;

namespace EduFlyUp.Repositories;

public interface IInstructorStudentRepository
{
    Task<List<InstructorStudentCourse>> CoursesAsync(InstructorIdentity instructor);
    Task<InstructorStudentRoster?> RosterAsync(InstructorIdentity instructor, int courseId, int page, string? search = null, EnrollmentStatus? status = null);
}

public sealed class InstructorStudentRepository(AppDbContext db) : IInstructorStudentRepository
{
    public Task<List<InstructorStudentCourse>> CoursesAsync(InstructorIdentity instructor) => InstructorStudentDAO.Instance.CoursesAsync(db, instructor);
    public Task<InstructorStudentRoster?> RosterAsync(InstructorIdentity instructor, int courseId, int page, string? search = null, EnrollmentStatus? status = null)
        => InstructorStudentDAO.Instance.RosterAsync(db, instructor, courseId, page, search, status);
}
