using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.DataAccess;

public sealed class InstructorStudentDAO : SingletonBase<InstructorStudentDAO>
{
    private static IQueryable<Course> Owned(AppDbContext db, InstructorIdentity instructor) => db.Courses.AsNoTracking()
        .Where(c => c.InstructorId == instructor.UserId || c.InstructorId == instructor.UserName);

    public Task<List<InstructorStudentCourse>> CoursesAsync(AppDbContext db, InstructorIdentity instructor)
        => Owned(db, instructor).OrderBy(c => c.Title).ThenBy(c => c.Id)
            .Select(c => new InstructorStudentCourse(c.Id, c.Title, c.Enrollments.Count)).ToListAsync();

    public async Task<InstructorStudentRoster?> RosterAsync(AppDbContext db, InstructorIdentity instructor, int courseId, int page)
    {
        var course = await Owned(db, instructor).Where(c => c.Id == courseId).Select(c => new { c.Id, c.Title }).SingleOrDefaultAsync();
        if (course is null) return null;
        var query = from enrollment in db.Enrollments.AsNoTracking()
                    where enrollment.CourseId == courseId
                    join user in db.Users.AsNoTracking() on enrollment.UserId equals user.Id into users
                    from user in users.DefaultIfEmpty()
                    select new { EnrollmentId = enrollment.Id, enrollment.UserId,
                        FullName = user == null ? "Tài khoản không còn tồn tại" : user.FullName != "" ? user.FullName : "Chưa cập nhật họ tên",
                        Email = user == null ? null : user.Email, enrollment.EnrolledAt, enrollment.Status, enrollment.CompletedAt };
        var total = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (total + InstructorStudentRoster.PageSize - 1) / InstructorStudentRoster.PageSize));
        var items = await query.OrderByDescending(e => e.EnrolledAt).ThenByDescending(e => e.EnrollmentId)
            .Skip((page - 1) * InstructorStudentRoster.PageSize).Take(InstructorStudentRoster.PageSize)
            .Select(e => new InstructorStudentRow(e.EnrollmentId, e.UserId, e.FullName, e.Email, e.EnrolledAt, e.Status, e.CompletedAt)).ToListAsync();
        return new(course.Id, course.Title, items, page, total);
    }
}
