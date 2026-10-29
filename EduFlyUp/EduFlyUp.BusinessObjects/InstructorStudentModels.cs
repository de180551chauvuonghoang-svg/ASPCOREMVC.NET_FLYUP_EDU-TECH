namespace EduFlyUp.BusinessObjects;

public sealed record InstructorIdentity(string UserId, string UserName);
public sealed record InstructorStudentCourse(int Id, string Title, int EnrollmentCount);
public sealed record InstructorStudentRow(int EnrollmentId, string UserId, string FullName, string? Email,
    DateTime EnrolledAt, EnrollmentStatus Status, DateTime? CompletedAt);
public sealed record InstructorStudentRoster(int CourseId, string CourseTitle, IReadOnlyList<InstructorStudentRow> Students,
    int Page, int TotalCount, string? Search = null, EnrollmentStatus? StatusFilter = null)
{
    public const int PageSize = 10;
    public int TotalPages => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
}
