using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;

namespace EduFlyUp.Repositories;

public interface IInstructorStudentRepository
{
    Task<List<InstructorStudentCourse>> CoursesAsync(InstructorIdentity instructor);
    Task<InstructorStudentRoster?> RosterAsync(InstructorIdentity instructor, int courseId, int page);
}

public sealed class InstructorStudentRepository(AppDbContext db) : IInstructorStudentRepository
{
    public Task<List<InstructorStudentCourse>> CoursesAsync(InstructorIdentity instructor) => InstructorStudentDAO.Instance.CoursesAsync(db, instructor);
    public Task<InstructorStudentRoster?> RosterAsync(InstructorIdentity instructor, int courseId, int page) => InstructorStudentDAO.Instance.RosterAsync(db, instructor, courseId, page);
}
