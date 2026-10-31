using System.Security.Claims;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

[Authorize(Roles = "Instructor")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class InstructorRevenueController(IInstructorRevenueService revenue) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, string? search = null)
    {
        if (!ModelState.IsValid || search?.Length > 100) return BadRequest();
        var instructor = new InstructorIdentity(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", User.Identity?.Name ?? "");
        return View(await revenue.ReportAsync(instructor, page, search));
    }
}
