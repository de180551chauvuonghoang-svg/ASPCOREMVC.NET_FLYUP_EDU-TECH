using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using EduFlyUp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlyUp.Tests;

public sealed class InstructorSummaryTests
{
    [SqlServerFact]
    public async Task Summary_counts_owned_enrollments_and_distinct_students_across_courses_at_sql_server()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = new Course { Title = "Owned by ID", InstructorId = "owner-id" };
        var second = new Course { Title = "Owned by legacy email", InstructorId = "owner@example.test", Status = CourseStatus.Archived };
        var foreign = new Course { Title = "Foreign", InstructorId = "someone-else" };
        db.Enrollments.AddRange(
            new Enrollment { Course = first, UserId = "same-student", Status = EnrollmentStatus.Active },
            new Enrollment { Course = second, UserId = "same-student", Status = EnrollmentStatus.Completed },
            new Enrollment { Course = second, UserId = "missing-profile", Status = EnrollmentStatus.Cancelled },
            new Enrollment { Course = foreign, UserId = "foreign-student" });
        await db.SaveChangesAsync();
        var service = new InstructorStudentService(new InstructorStudentRepository(db));
        var summary = await service.SummaryAsync(new("owner-id", "owner@example.test"));
        Assert.Equal(3, summary.TotalEnrollments);
        Assert.Equal(2, summary.UniqueStudents);
        Assert.Equal(1, summary.ActiveEnrollments);
        Assert.Equal(1, summary.CompletedEnrollments);
        Assert.Equal(1, summary.CancelledEnrollments);
        Assert.Equal(summary.TotalEnrollments, summary.ActiveEnrollments + summary.CompletedEnrollments + summary.CancelledEnrollments);
        Assert.Equal(new InstructorEnrollmentSummary(0, 0), await service.SummaryAsync(new("empty", "empty@example.test")));
        Assert.Equal(new InstructorEnrollmentSummary(0, 0), await service.SummaryAsync(new("", "owner@example.test")));
    }
}
