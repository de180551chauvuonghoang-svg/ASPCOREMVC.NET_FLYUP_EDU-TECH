using System.Security.Claims;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using EduFlyUp.Web.Models.Instructor;
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
    public async Task<IActionResult> Export(int courseId, string? search = null, EnrollmentStatus? status = null)
    {
        if (!ModelState.IsValid || courseId <= 0 || search?.Length > 100 || (status.HasValue && !Enum.IsDefined(status.Value))) return BadRequest();
        try
        {
            var report = await students.ExportAsync(Instructor, courseId, search, status);
            if (report is null) return NotFound();
            return File(InstructorStudentWorkbook.Create(report),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"HocVien-KhoaHoc-{courseId}.xlsx");
        }
        catch (StudentExportLimitException)
        {
            return UnprocessableEntity("Báo cáo vượt giới hạn 10.000 học viên hoặc 50.000 dòng lịch sử. Vui lòng thu hẹp bộ lọc và xuất lại.");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] int id, [FromQuery] int courseId, int page = 1)
    {
        if (!ModelState.IsValid || id <= 0 || courseId <= 0) return BadRequest();
        var detail = await students.DetailAsync(Instructor, courseId, id, page);
        return detail is null ? NotFound() : View(detail);
    }
}
