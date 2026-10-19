using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.Repositories;

public interface IAdminCourseRepository
{
    Task<AdminCoursePage> SearchAsync(AdminCourseQuery query);
    Task<AdminCourseDetail?> GetDetailAsync(int id);
    Task<bool> SetPublicationAsync(int id, bool published);
}

// Scoped context: SQL filtering/paging and publication-only updates share the
// configured application database without changing the existing singleton DAOs.
public sealed class AdminCourseRepository(AppDbContext context) : IAdminCourseRepository
{
    public async Task<AdminCoursePage> SearchAsync(AdminCourseQuery query)
    {
        const int pageSize = 10;
        var courses = Filter(context, query);
        var count = await courses.CountAsync();
        var page = Math.Clamp(query.Page, 1, Math.Max(1, (count - 1) / pageSize + 1));
        var items = await PageQuery(context, courses, (page - 1) * pageSize, pageSize).ToListAsync();
        var instructors = await InstructorQuery(context).ToListAsync();
        return new AdminCoursePage(items, instructors, count, page, pageSize);
    }

    internal static IQueryable<Course> Filter(AppDbContext context, AdminCourseQuery query)
    {
        var courses = context.Courses.AsNoTracking();
        if (!string.IsNullOrEmpty(query.Search)) courses = courses.Where(c => c.Title.Contains(query.Search));
        if (!string.IsNullOrEmpty(query.Instructor))
        {
            var instructor = query.Instructor.ToUpperInvariant();
            courses = courses.Where(c => c.InstructorId.ToUpper() == instructor);
        }
        if (query.Level.HasValue) courses = courses.Where(c => c.Level == query.Level.Value);
        if (query.IsPublished.HasValue) courses = courses.Where(c => c.IsPublished == query.IsPublished.Value);
        return courses;
    }

    internal static IQueryable<AdminCourseRow> PageQuery(AppDbContext context, IQueryable<Course> courses, int skip, int take)
    {
        var slice = courses.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
            .Skip(skip).Take(take);
        return from course in slice
            join user in context.Users.AsNoTracking() on course.InstructorId.ToUpper() equals user.NormalizedUserName into owners
            from owner in owners.DefaultIfEmpty()
            orderby course.CreatedAt descending, course.Id descending
            select new AdminCourseRow(course.Id, course.Title, course.InstructorId,
                owner == null ? null : owner.FullName, owner == null ? null : owner.Email,
                course.Level, course.Price, course.IsPublished, course.CreatedAt, course.Lessons.Count);
    }

    internal static IQueryable<AdminInstructorOption> InstructorQuery(AppDbContext context)
    {
        // Use course owners, including legacy seed values, rather than only users
        // currently assigned Instructor: Admin-created courses must be filterable too.
        return from name in context.Courses.AsNoTracking().Where(c => c.InstructorId != "").Select(c => c.InstructorId).Distinct()
            join user in context.Users.AsNoTracking() on name.ToUpper() equals user.NormalizedUserName into owners
            from owner in owners.DefaultIfEmpty()
            orderby owner != null && owner.FullName != "" ? owner.FullName : name, name
            select new AdminInstructorOption(name, owner != null && owner.FullName != ""
                ? owner.FullName + " (" + name + ")" : name);
    }

    public async Task<AdminCourseDetail?> GetDetailAsync(int id)
    {
        var course = await context.Courses.AsNoTracking().Include(c => c.Lessons.OrderBy(l => l.Order).ThenBy(l => l.Id))
            .SingleOrDefaultAsync(c => c.Id == id);
        if (course is null) return null;
        var name = course.InstructorId.ToUpperInvariant();
        var owner = await context.Users.AsNoTracking().Where(u => u.NormalizedUserName == name)
            .Select(u => new { u.FullName, u.Email }).SingleOrDefaultAsync();
        return new AdminCourseDetail(course, owner?.FullName, owner?.Email);
    }

    public async Task<bool> SetPublicationAsync(int id, bool published)
    {
        var course = await context.Courses.FindAsync(id);
        if (course is null) return false;
        course.IsPublished = published;
        // Tracked scalar update: preserve content, owner, lessons and enrollments.
        await context.SaveChangesAsync();
        return true;
    }
}
