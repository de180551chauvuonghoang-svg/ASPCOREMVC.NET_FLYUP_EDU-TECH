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

    public async Task<InstructorEnrollmentSummary> SummaryAsync(AppDbContext db, InstructorIdentity instructor)
    {
        var counts = await db.Enrollments.AsNoTracking()
            .Where(e => Owned(db, instructor).Any(c => c.Id == e.CourseId))
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Count(), Unique = g.Select(e => e.UserId).Distinct().Count() })
            .SingleOrDefaultAsync();
        return new(counts?.Total ?? 0, counts?.Unique ?? 0);
    }

    public async Task<InstructorStudentRoster?> RosterAsync(AppDbContext db, InstructorIdentity instructor, int courseId, int page,
        string? search = null, EnrollmentStatus? status = null, bool allRows = false)
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
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.ToUpperInvariant();
            query = query.Where(e => e.FullName.ToUpper().Contains(keyword) || (e.Email != null && e.Email.ToUpper().Contains(keyword)));
        }
        if (status.HasValue) query = query.Where(e => e.Status == status);
        var total = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (total + InstructorStudentRoster.PageSize - 1) / InstructorStudentRoster.PageSize));
        var items = await query.OrderByDescending(e => e.EnrolledAt).ThenByDescending(e => e.EnrollmentId)
            .Skip(allRows ? 0 : (page - 1) * InstructorStudentRoster.PageSize).Take(allRows ? 10001 : InstructorStudentRoster.PageSize)
            .Select(e => new InstructorStudentRow(e.EnrollmentId, e.UserId, e.FullName, e.Email, e.EnrolledAt, e.Status, e.CompletedAt)).ToListAsync();
        return new(course.Id, course.Title, items, page, total, search, status);
    }

    public async Task<InstructorStudentExport?> ExportAsync(AppDbContext db, InstructorIdentity instructor, int courseId,
        string? search, EnrollmentStatus? status)
    {
        var roster = await RosterAsync(db, instructor, courseId, 1, search, status, allRows: true);
        if (roster is null) return null;
        if (roster.TotalCount > 10000) throw new StudentExportLimitException();
        var userIds = roster.Students.Select(s => s.UserId).Distinct().ToArray();
        var attempts = await db.QuizAttempts.AsNoTracking()
            .Where(a => a.Quiz.CourseId == courseId && userIds.Contains(a.UserId))
            .OrderBy(a => a.UserId).ThenBy(a => a.QuizId).ThenBy(a => a.StartedAt).ThenBy(a => a.Id)
            .Select(a => new { a.UserId, a.QuizId, Row = new InstructorQuizAttemptRow(a.Id, a.Quiz.Title, a.Quiz.IsCAT,
                a.CompletedAt != null ? (decimal?)a.Score : null, a.CompletedAt != null ? (bool?)a.Passed : null,
                a.StartedAt, a.CompletedAt) }).Take(50001).ToListAsync();
        if (attempts.Count > 50000) throw new StudentExportLimitException();
        var byUser = attempts.ToLookup(a => a.UserId);
        var rows = roster.Students.SelectMany(s => byUser[s.UserId].Select(a =>
            new InstructorStudentAttemptExport(s.EnrollmentId, s.UserId, s.FullName, s.Email, a.QuizId, a.Row))).Take(50001).ToList();
        if (rows.Count > 50000) throw new StudentExportLimitException();
        return new(roster, rows);
    }

    public async Task<InstructorStudentDetail?> DetailAsync(AppDbContext db, InstructorIdentity instructor, int courseId, int enrollmentId, int page)
    {
        var enrollment = await (from e in db.Enrollments.AsNoTracking()
                                join course in Owned(db, instructor) on e.CourseId equals course.Id
                                where e.Id == enrollmentId && course.Id == courseId
                                select new { e.Id, e.UserId, e.EnrolledAt, e.Status, e.CompletedAt, CourseTitle = course.Title }).SingleOrDefaultAsync();
        if (enrollment is null) return null;
        var user = await db.Users.AsNoTracking().Where(u => u.Id == enrollment.UserId).Select(u => new { u.FullName, u.Email }).SingleOrDefaultAsync();
        var student = new InstructorStudentRow(enrollment.Id, enrollment.UserId,
            user is null ? "Tài khoản không còn tồn tại" : string.IsNullOrWhiteSpace(user.FullName) ? "Chưa cập nhật họ tên" : user.FullName,
            user?.Email, enrollment.EnrolledAt, enrollment.Status, enrollment.CompletedAt);
        var attempts = db.QuizAttempts.AsNoTracking().Where(a => a.UserId == enrollment.UserId && a.Quiz.CourseId == courseId);
        var summary = await attempts.GroupBy(_ => 1).Select(g => new
        {
            Total = g.Count(), Submitted = g.Count(a => a.CompletedAt != null),
            Passed = g.Count(a => a.CompletedAt != null && a.Passed), Last = g.Max(a => a.CompletedAt)
        }).SingleOrDefaultAsync();
        var count = summary?.Total ?? 0;
        page = Math.Clamp(page, 1, Math.Max(1, (count + InstructorStudentDetail.PageSize - 1) / InstructorStudentDetail.PageSize));
        var items = await attempts.OrderByDescending(a => a.StartedAt).ThenByDescending(a => a.Id)
            .Skip((page - 1) * InstructorStudentDetail.PageSize).Take(InstructorStudentDetail.PageSize)
            .Select(a => new InstructorQuizAttemptRow(a.Id, a.Quiz.Title, a.Quiz.IsCAT,
                a.CompletedAt != null ? (decimal?)a.Score : null, a.CompletedAt != null ? (bool?)a.Passed : null,
                a.StartedAt, a.CompletedAt)).ToListAsync();
        return new(courseId, enrollment.CourseTitle, student, items, page, count,
            summary?.Submitted ?? 0, summary?.Passed ?? 0, summary?.Last);
    }
}
