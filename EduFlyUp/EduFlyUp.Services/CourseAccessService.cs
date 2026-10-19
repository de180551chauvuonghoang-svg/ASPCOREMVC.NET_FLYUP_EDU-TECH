using System.Security.Claims;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;

namespace EduFlyUp.Services;

public interface ICourseAccessService
{
    bool CanManageCourse(Course course, ClaimsPrincipal user);
    Task<bool> CanViewCourseAsync(Course course, ClaimsPrincipal user);
    Task<bool> CanLearnCourseAsync(Course course, ClaimsPrincipal user);
}

public sealed class CourseAccessService(IEnrollmentRepository enrollments) : ICourseAccessService
{
    public bool CanManageCourse(Course course, ClaimsPrincipal user)
        => user.Identity?.IsAuthenticated == true &&
           (user.IsInRole("Admin") ||
            (user.IsInRole("Instructor") &&
             !string.IsNullOrWhiteSpace(user.Identity.Name) &&
             !string.IsNullOrWhiteSpace(course.InstructorId) &&
             string.Equals(course.InstructorId, user.Identity.Name, StringComparison.OrdinalIgnoreCase)));

    public async Task<bool> CanViewCourseAsync(Course course, ClaimsPrincipal user)
        => course.IsPublished || await CanLearnCourseAsync(course, user);

    // Learning endpoints must use this method, not the public metadata rule above.
    public async Task<bool> CanLearnCourseAsync(Course course, ClaimsPrincipal user)
    {
        if (CanManageCourse(course, user)) return true;
        if (user.Identity?.IsAuthenticated != true) return false;

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId)) return false;

        var userEnrollments = await enrollments.GetByUserIdAsync(userId);
        return userEnrollments.Any(e => e.CourseId == course.Id &&
            e.Status is EnrollmentStatus.Active or EnrollmentStatus.Completed);
    }
}
