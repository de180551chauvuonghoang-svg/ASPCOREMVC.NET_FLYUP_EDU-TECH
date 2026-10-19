using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using EduFlyUp.Web.Models.Admin;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ICourseService _courseService;
    private readonly ILessonService _lessonService;
    private readonly IAdminCourseService _adminCourses;
    private readonly IAdminUserService _adminUsers;

    public AdminController(ICourseService courseService, ILessonService lessonService, IAdminCourseService adminCourses, IAdminUserService adminUsers)
    {
        _courseService = courseService;
        _lessonService = lessonService;
        _adminCourses = adminCourses;
        _adminUsers = adminUsers;
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

    [HttpGet]
    public async Task<IActionResult> Users([FromQuery] AdminUserFilter filter)
    {
        if (!ModelState.IsValid) return BadRequest("Bộ lọc không hợp lệ.");
        var query = filter.ToQuery();
        var result = await _adminUsers.SearchAsync(query);
        return View(new AdminUserListModel(query with { Page = result.Page }, result));
    }

    [HttpGet]
    public async Task<IActionResult> UserDetails([FromRoute] string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        var user = await _adminUsers.GetDetailAsync(id);
        return user is null ? NotFound() : View(user);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> DeactivateUser([FromRoute] string id, string? returnUrl = null)
        => SetUserActive(id, false, returnUrl);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> ActivateUser([FromRoute] string id, string? returnUrl = null)
        => SetUserActive(id, true, returnUrl);

    private async Task<IActionResult> SetUserActive(string id, bool active, string? returnUrl)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(actorId)) return Forbid();
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        var result = await _adminUsers.SetActiveAsync(actorId, id, active);
        if (result == AdminUserChangeResult.ActorNotAllowed) return Forbid();
        if (result == AdminUserChangeResult.NotFound) return NotFound();
        var success = result is AdminUserChangeResult.Changed or AdminUserChangeResult.Unchanged;
        TempData[success ? "SuccessMessage" : "ErrorMessage"] = result switch
        {
            AdminUserChangeResult.Changed => active ? "Đã mở khóa tài khoản. Người dùng cần đăng nhập lại." : "Đã khóa tài khoản. Phiên đăng nhập mất quyền từ request tiếp theo.",
            AdminUserChangeResult.Unchanged => active ? "Tài khoản đã ở trạng thái hoạt động." : "Tài khoản đã bị khóa.",
            AdminUserChangeResult.SelfDeactivation => "Bạn không thể tự khóa tài khoản của mình.",
            AdminUserChangeResult.LastActiveAdmin => "Không thể khóa Admin hoạt động cuối cùng.",
            AdminUserChangeResult.Conflict => "Dữ liệu tài khoản vừa thay đổi. Vui lòng tải lại và thử lại.",
            _ => "Chưa thể cập nhật tài khoản. Vui lòng thử lại sau."
        };
        if (Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl!);
        return RedirectToAction(nameof(UserDetails), new { id });
    }
}
