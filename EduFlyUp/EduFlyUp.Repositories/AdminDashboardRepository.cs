using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.Repositories;

public interface IAdminDashboardRepository
{
    Task<AdminDashboardStats> GetStatsAsync();
}

public sealed class AdminDashboardRepository(AppDbContext context) : IAdminDashboardRepository
{
    internal sealed record UserCounts(int Total, int Active);
    internal sealed record CourseCounts(int Total, int Published);

    internal static IQueryable<UserCounts> UsersQuery(AppDbContext context)
        => context.Users.AsNoTracking().GroupBy(_ => 1)
            .Select(group => new UserCounts(group.Count(), group.Count(user => user.IsActive)));

    internal static IQueryable<CourseCounts> CoursesQuery(AppDbContext context)
        => context.Courses.AsNoTracking().GroupBy(_ => 1)
            .Select(group => new CourseCounts(group.Count(), group.Count(course => course.IsPublished)));

    public async Task<AdminDashboardStats> GetStatsAsync()
    {
        var users = await UsersQuery(context).SingleOrDefaultAsync() ?? new UserCounts(0, 0);
        var courses = await CoursesQuery(context).SingleOrDefaultAsync() ?? new CourseCounts(0, 0);
        var lessons = await context.Lessons.CountAsync();
        return new AdminDashboardStats(users.Total, users.Active, users.Total - users.Active,
            courses.Total, courses.Published, courses.Total - courses.Published, lessons);
    }
}
