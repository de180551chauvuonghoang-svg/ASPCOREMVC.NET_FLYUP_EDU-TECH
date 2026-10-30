using System.Security.Claims;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

[Authorize(Roles = "Instructor")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class InstructorPayoutsController(IInstructorPayoutService payouts) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        if (!ModelState.IsValid) return BadRequest();
        var instructor = new InstructorIdentity(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", User.Identity?.Name ?? "");
        return View(await payouts.HistoryAsync(instructor, page));
    }
}
