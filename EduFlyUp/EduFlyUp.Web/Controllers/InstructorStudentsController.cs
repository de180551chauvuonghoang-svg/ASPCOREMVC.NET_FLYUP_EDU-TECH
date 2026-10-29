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
    public async Task<IActionResult> Index(int? courseId, int page = 1)
    {
        if (!ModelState.IsValid) return BadRequest();
        if (!courseId.HasValue) return RedirectToAction(nameof(Courses));
        var roster = await students.RosterAsync(Instructor, courseId.Value, page);
        return roster is null ? NotFound() : View(roster);
    }
}
