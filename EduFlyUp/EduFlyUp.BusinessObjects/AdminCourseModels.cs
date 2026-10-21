namespace EduFlyUp.BusinessObjects;

public sealed record AdminCourseQuery(string? Search = null, string? Instructor = null,
    CourseLevel? Level = null, bool? IsPublished = null, int Page = 1);

public sealed record AdminInstructorOption(string UserName, string DisplayName);

public sealed record AdminCourseRow(int Id, string Title, string InstructorId,
    string? InstructorName, string? InstructorEmail, CourseLevel Level, decimal Price,
    bool IsPublished, DateTime CreatedAt, int LessonCount);

public sealed record AdminCoursePage(IReadOnlyList<AdminCourseRow> Items,
    IReadOnlyList<AdminInstructorOption> Instructors, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => Math.Max(1, (TotalCount - 1) / PageSize + 1);
}

public sealed record AdminCourseDetail(Course Course, string? InstructorName, string? InstructorEmail);
