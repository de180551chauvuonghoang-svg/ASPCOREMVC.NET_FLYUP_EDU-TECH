using System.Security.Claims;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;

namespace EduFlyUp.Tests;

public sealed class CourseAccessTests
{
    [Theory]
    [InlineData("Admin", "other@example.test", true)]
    [InlineData("Instructor", "owner@example.test", true)]
    [InlineData("Instructor", "OWNER@example.test", true)]
    [InlineData("Instructor", "other@example.test", false)]
    [InlineData("Student", "owner@example.test", false)]
    [InlineData("Teacher", "owner@example.test", false)]
    [InlineData("Instructor", "", false)]
    public void Management_requires_admin_or_authenticated_instructor_ownership(string role, string name, bool expected)
    {
        var service = new CourseAccessService(new TestEnrollmentRepository());
        var course = new Course { InstructorId = "owner@example.test" };
        Assert.Equal(expected, service.CanManageCourse(course, User(role, name)));
    }

    [Fact]
    public async Task Guest_can_only_view_published_courses_and_never_triggers_an_enrollment_query()
    {
        var repo = new TestEnrollmentRepository();
        var service = new CourseAccessService(repo);
        var guest = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.True(await service.CanViewCourseAsync(new Course { IsPublished = true }, guest));
        Assert.False(await service.CanViewCourseAsync(new Course(), guest));
        Assert.False(service.CanManageCourse(new Course { InstructorId = "owner@example.test" }, guest));
        Assert.Equal(0, repo.Queries);
    }

    [Theory]
    [InlineData(EnrollmentStatus.Active, true)]
    [InlineData(EnrollmentStatus.Completed, true)]
    [InlineData(EnrollmentStatus.Cancelled, false)]
    [InlineData((EnrollmentStatus)999, false)]
    public async Task Hidden_course_preserves_only_valid_learning_access(EnrollmentStatus status, bool expected)
    {
        var repo = new TestEnrollmentRepository();
        repo.Enrollments.Add(new Enrollment { UserId = "student-id", CourseId = 7, Status = status });
        var service = new CourseAccessService(repo);
        Assert.Equal(expected, await service.CanViewCourseAsync(new Course { Id = 7 }, User("Student", "student@example.test", "student-id")));
    }

    [Fact]
    public async Task Public_metadata_does_not_grant_access_to_learning_content()
    {
        var repo = new TestEnrollmentRepository();
        var service = new CourseAccessService(repo);
        var course = new Course { Id = 7, IsPublished = true };
        var guest = new ClaimsPrincipal(new ClaimsIdentity());
        var student = User("Student", "student@example.test", "student-id");
        Assert.True(await service.CanViewCourseAsync(course, guest));
        Assert.False(await service.CanLearnCourseAsync(course, guest));
        Assert.False(await service.CanLearnCourseAsync(course, student));
        repo.Enrollments.Add(new Enrollment { CourseId = 7, UserId = "student-id", Status = EnrollmentStatus.Active });
        Assert.True(await service.CanLearnCourseAsync(course, student));
    }

    [Fact]
    public async Task Enrollment_must_match_both_the_user_id_and_course_id()
    {
        var repo = new TestEnrollmentRepository();
        repo.Enrollments.Add(new Enrollment { UserId = "another-id", CourseId = 7 });
        repo.Enrollments.Add(new Enrollment { UserId = "student-id", CourseId = 8 });
        var service = new CourseAccessService(repo);
        Assert.False(await service.CanViewCourseAsync(new Course { Id = 7 }, User("Student", "student@example.test", "student-id")));
    }

    [Theory]
    [InlineData("Admin", "other@example.test")]
    [InlineData("Instructor", "owner@example.test")]
    public async Task Admin_and_owner_can_preview_hidden_courses_without_enrollment(string role, string name)
    {
        var repo = new TestEnrollmentRepository();
        var service = new CourseAccessService(repo);
        Assert.True(await service.CanViewCourseAsync(new Course { InstructorId = "owner@example.test" }, User(role, name)));
        Assert.Equal(0, repo.Queries);
    }

    private static ClaimsPrincipal User(string role, string name, string id = "id") => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, name), new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Role, role)], "test"));
}
