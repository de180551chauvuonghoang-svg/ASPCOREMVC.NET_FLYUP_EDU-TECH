using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.Repositories;

public sealed class AdminUserPersistenceException(Exception inner)
    : Exception("Unable to persist the account status change.", inner);

public interface IAdminUserRepository
{
    Task<AdminUserPage> SearchAsync(AdminUserQuery query);
    Task<AdminUserDetail?> GetDetailAsync(string id);
    void RefreshForStatusChange();
    Task<AdminUserStatus?> GetStatusAsync(string id);
    Task<int> CountActiveAdminsAsync();
    Task<bool> SaveActiveStateAsync(string id, bool active);
}

public sealed class AdminUserRepository(AppDbContext context, UserManager<ApplicationUser> users) : IAdminUserRepository
{
    internal static IQueryable<ApplicationUser> Filter(AppDbContext context, AdminUserQuery query)
    {
        var result = context.Users.AsNoTracking();
        if (!string.IsNullOrEmpty(query.Search))
            result = result.Where(u => u.FullName.Contains(query.Search) || (u.Email != null && u.Email.Contains(query.Search)));
        if (query.IsActive.HasValue) result = result.Where(u => u.IsActive == query.IsActive.Value);
        if (!string.IsNullOrEmpty(query.Role))
        {
            var role = query.Role.ToUpperInvariant();
            result = result.Where(u => (from membership in context.UserRoles
                join r in context.Roles on membership.RoleId equals r.Id
                where membership.UserId == u.Id && r.NormalizedName == role select membership.UserId).Any());
        }
        return result;
    }

    public async Task<AdminUserPage> SearchAsync(AdminUserQuery query)
    {
        const int pageSize = 10;
        var filtered = Filter(context, query);
        var count = await filtered.CountAsync();
        var page = Math.Clamp(query.Page, 1, Math.Max(1, (count - 1) / pageSize + 1));
        // Never select password hashes, stamps or tokens for list/detail DTOs.
        var slice = await filtered.OrderByDescending(u => u.CreatedAt).ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new { u.Id, u.FullName, u.Email, u.IsActive, u.CreatedAt }).ToListAsync();
        var ids = slice.Select(u => u.Id).ToArray();
        var memberships = await (from membership in context.UserRoles.AsNoTracking()
            join role in context.Roles.AsNoTracking() on membership.RoleId equals role.Id
            where ids.Contains(membership.UserId)
            orderby role.Name select new { membership.UserId, role.Name }).ToListAsync();
        var roles = await context.Roles.AsNoTracking().Where(r => r.Name != null).OrderBy(r => r.Name)
            .Select(r => r.Name!).ToListAsync();
        return new AdminUserPage(slice.Select(u => new AdminUserRow(u.Id, u.FullName, u.Email,
            u.IsActive, u.CreatedAt, memberships.Where(m => m.UserId == u.Id).Select(m => m.Name!).ToList())).ToList(),
            roles, count, page, pageSize);
    }

    public async Task<AdminUserDetail?> GetDetailAsync(string id)
    {
        var user = await context.Users.AsNoTracking().Where(u => u.Id == id).Select(u => new
        { u.Id, u.FullName, u.Email, u.UserName, u.IsActive, u.CreatedAt, u.AvatarUrl, u.Bio, u.PhoneNumber }).SingleOrDefaultAsync();
        if (user is null) return null;
        var roles = await (from membership in context.UserRoles.AsNoTracking()
            join role in context.Roles.AsNoTracking() on membership.RoleId equals role.Id
            where membership.UserId == id orderby role.Name select role.Name!).ToListAsync();
        var userName = user.UserName?.ToUpperInvariant();
        var courses = await context.Courses.AsNoTracking()
            .Where(c => userName != null && userName != "" && c.InstructorId.ToUpper() == userName)
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
            .Select(c => new AdminUserCourse(c.Id, c.Title, c.IsPublished)).ToListAsync();
        var enrollments = await (from enrollment in context.Enrollments.AsNoTracking()
            join course in context.Courses.AsNoTracking() on enrollment.CourseId equals course.Id
            where enrollment.UserId == id orderby enrollment.EnrolledAt descending, enrollment.Id descending
            select new AdminUserEnrollment(course.Id, course.Title, enrollment.Status, enrollment.EnrolledAt)).ToListAsync();
        return new AdminUserDetail(new AdminUserRow(user.Id, user.FullName, user.Email, user.IsActive, user.CreatedAt, roles),
            user.UserName, user.AvatarUrl, user.Bio, user.PhoneNumber, courses, enrollments);
    }

    // Cookie validation may have tracked the actor BEFORE waiting for the SQL
    // lock. Clear that snapshot so authorization inside the transaction is fresh.
    public void RefreshForStatusChange() => context.ChangeTracker.Clear();

    public async Task<AdminUserStatus?> GetStatusAsync(string id)
    {
        var user = await users.FindByIdAsync(id);
        return user is null ? null : new AdminUserStatus(user.Id, user.IsActive, await users.IsInRoleAsync(user, "Admin"));
    }

    public Task<int> CountActiveAdminsAsync() => (from user in context.Users
        join membership in context.UserRoles on user.Id equals membership.UserId
        join role in context.Roles on membership.RoleId equals role.Id
        where user.IsActive && role.NormalizedName == "ADMIN" select user.Id).Distinct().CountAsync();

    public async Task<bool> SaveActiveStateAsync(string id, bool active)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return false;
        user.IsActive = active;
        // UpdateSecurityStampAsync persists the changed flag and stamp together
        // through Identity, with its normal optimistic concurrency protection.
        try
        {
            var result = active ? await users.UpdateAsync(user) : await users.UpdateSecurityStampAsync(user);
            return result.Succeeded;
        }
        catch (DbUpdateException ex) { throw new AdminUserPersistenceException(ex); }
    }
}
