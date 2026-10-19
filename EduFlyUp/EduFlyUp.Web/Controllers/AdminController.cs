using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using EduFlyUp.Web.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ICourseService _courseService;
    private readonly ILessonService _lessonService;
    private readonly IAdminCourseService _adminCourses;

    public AdminController(ICourseService courseService, ILessonService lessonService, IAdminCourseService adminCourses)
    {
        _courseService = courseService;
        _lessonService = lessonService;
        _adminCourses = adminCourses;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var courses = (await _courseService.GetAllCoursesAsync()).ToList();

        ViewBag.TotalCourses = courses.Count;
        ViewBag.PublishedCourses = courses.Count(c => c.IsPublished);
        ViewBag.TotalLessons = await _lessonService.GetTotalLessonsCountAsync();

        return View(courses);
    }

    [HttpGet]
    public IActionResult Dashboard()
    {
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Courses([FromQuery] AdminCourseFilter filter)
    {
        if (!ModelState.IsValid) return BadRequest("Bộ lọc không hợp lệ.");
        var query = filter.ToQuery();
        var result = await _adminCourses.SearchAsync(query);
        return View(new AdminCourseListModel(query with { Page = result.Page }, result));
    }

    [HttpGet]
    public async Task<IActionResult> CourseDetails([FromRoute] int id)
    {
        var detail = await _adminCourses.GetDetailAsync(id);
        return detail is null ? NotFound() : View(detail);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> EnableCourse([FromRoute] int id, string? returnUrl = null)
        => SetPublication(id, true, returnUrl);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> DisableCourse([FromRoute] int id, string? returnUrl = null)
        => SetPublication(id, false, returnUrl);

    private async Task<IActionResult> SetPublication(int id, bool published, string? returnUrl)
    {
        if (!await _adminCourses.SetPublicationAsync(id, published)) return NotFound();
        TempData["SuccessMessage"] = published ? "Đã công khai khóa học." : "Đã ẩn khóa học. Nội dung và ghi danh được giữ nguyên.";
        if (Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl!);
        return RedirectToAction(nameof(CourseDetails), new { id });
    }
}
