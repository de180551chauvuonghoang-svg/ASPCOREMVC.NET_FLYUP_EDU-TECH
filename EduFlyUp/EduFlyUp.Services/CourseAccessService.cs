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
    {
        if (user.Identity?.IsAuthenticated != true) return false;
        if (user.IsInRole("Admin")) return true;

        if (user.IsInRole("Instructor") && !string.IsNullOrWhiteSpace(course.InstructorId))
        {
            if (!string.IsNullOrWhiteSpace(user.Identity.Name) &&
                string.Equals(course.InstructorId, user.Identity.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(userId) &&
                string.Equals(course.InstructorId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<bool> CanViewCourseAsync(Course course, ClaimsPrincipal user)
        => course.IsPublished || course.Status == CourseStatus.Approved || await CanLearnCourseAsync(course, user);

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
