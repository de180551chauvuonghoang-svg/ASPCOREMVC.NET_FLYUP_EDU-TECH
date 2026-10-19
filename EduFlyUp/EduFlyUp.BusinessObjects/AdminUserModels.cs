namespace EduFlyUp.BusinessObjects;

public sealed record AdminUserQuery(string? Search = null, string? Role = null, bool? IsActive = null, int Page = 1);
public sealed record AdminUserRow(string Id, string FullName, string? Email, bool IsActive,
    DateTime CreatedAt, IReadOnlyList<string> Roles);
public sealed record AdminUserPage(IReadOnlyList<AdminUserRow> Items, IReadOnlyList<string> Roles,
    int TotalCount, int Page, int PageSize)
{
    public int TotalPages => Math.Max(1, (TotalCount - 1) / PageSize + 1);
}
public sealed record AdminUserCourse(int Id, string Title, bool IsPublished);
public sealed record AdminUserEnrollment(int CourseId, string Title, EnrollmentStatus Status, DateTime EnrolledAt);
public sealed record AdminUserDetail(AdminUserRow User, string? UserName, string? AvatarUrl, string? Bio,
    string? PhoneNumber, IReadOnlyList<AdminUserCourse> Courses, IReadOnlyList<AdminUserEnrollment> Enrollments);
public sealed record AdminUserStatus(string Id, bool IsActive, bool IsAdmin);
public enum AdminUserChangeResult
{
    Changed, Unchanged, NotFound, SelfDeactivation, LastActiveAdmin, ActorNotAllowed, Conflict, Unavailable
}
