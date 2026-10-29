using System.Security.Claims;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

[Authorize(Roles = "Instructor")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class InstructorStudentsController(IInstructorStudentService students) : Controller
{
    private InstructorIdentity Instructor => new(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", User.Identity?.Name ?? "");
    [HttpGet]
    public async Task<IActionResult> Courses() => View(await students.CoursesAsync(Instructor));

    [HttpGet]
    public async Task<IActionResult> Index(int? courseId, int page = 1, string? search = null, EnrollmentStatus? status = null)
    {
        if (!ModelState.IsValid || search?.Length > 100 || (status.HasValue && !Enum.IsDefined(status.Value))) return BadRequest();
        if (!courseId.HasValue) return RedirectToAction(nameof(Courses));
        var roster = await students.RosterAsync(Instructor, courseId.Value, page, search, status);
        return roster is null ? NotFound() : View(roster);
    }

    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] int id, [FromQuery] int courseId, int page = 1)
    {
        if (!ModelState.IsValid || id <= 0 || courseId <= 0) return BadRequest();
        var detail = await students.DetailAsync(Instructor, courseId, id, page);
        return detail is null ? NotFound() : View(detail);
    }
}
