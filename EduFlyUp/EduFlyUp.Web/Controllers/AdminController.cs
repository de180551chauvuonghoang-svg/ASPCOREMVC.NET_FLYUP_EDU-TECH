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
    private readonly IAdminDashboardService _dashboard;
    private readonly IAdminCourseService _adminCourses;
    private readonly IAdminUserService _adminUsers;

    public AdminController(IAdminDashboardService dashboard, IAdminCourseService adminCourses, IAdminUserService adminUsers)
    {
        _dashboard = dashboard;
        _adminCourses = adminCourses;
        _adminUsers = adminUsers;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
        => View(new AdminDashboardModel(await _dashboard.GetStatsAsync()));

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
    public Task<IActionResult> DisableCourse([FromRoute] int id, string? rejectionReason = null, string? returnUrl = null)
        => SetPublication(id, false, returnUrl, rejectionReason);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectCourse([FromRoute] int id, string? rejectionReason = null, string? returnUrl = null)
    {
        var detail = await _adminCourses.GetDetailAsync(id);
        if (detail is null) return NotFound();

        if (ValidateMutableCourseState(detail.Course) is string error)
        {
            TempData["ErrorMessage"] = error;
            return RedirectAfterCourseAction(id, returnUrl);
        }

        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do từ chối khóa học để giảng viên biết cách chỉnh sửa.";
            return RedirectAfterCourseAction(id, returnUrl);
        }

        if (!await _adminCourses.RejectCourseAsync(id, rejectionReason)) return NotFound();
        TempData["SuccessMessage"] = "Đã từ chối khóa học và gửi lý do phản hồi cho giảng viên.";
        return RedirectAfterCourseAction(id, returnUrl);
    }

    private async Task<IActionResult> SetPublication(int id, bool published, string? returnUrl, string? rejectionReason = null)
    {
        var detail = await _adminCourses.GetDetailAsync(id);
        if (detail is null) return NotFound();

        if (published)
        {
            if (detail.Course.Lessons.Count == 0)
            {
                TempData["ErrorMessage"] = "Không thể công khai khóa học chưa có bài giảng nào.";
                return RedirectAfterCourseAction(id, returnUrl);
            }

            if (ValidateMutableCourseState(detail.Course, forPublication: true) is string error)
            {
                TempData["ErrorMessage"] = error;
                return RedirectAfterCourseAction(id, returnUrl);
            }
        }

        if (!await _adminCourses.SetPublicationAsync(id, published, rejectionReason)) return NotFound();
        TempData["SuccessMessage"] = published ? "Đã công khai khóa học." : "Đã ẩn khóa học. Nội dung và ghi danh được giữ nguyên.";
        return RedirectAfterCourseAction(id, returnUrl);
    }

    private static string? ValidateMutableCourseState(Course course, bool forPublication = false)
    {
        if (course.Status == CourseStatus.Draft)
            return "Khóa học này đang là bản nháp, giảng viên chưa gửi yêu cầu phê duyệt.";
        if (course.Status == CourseStatus.Archived)
            return forPublication
                ? "Khóa học này đã được giảng viên lưu trữ, không thể tự ý công khai."
                : "Khóa học này đã được giảng viên lưu trữ.";
        return null;
    }

    private IActionResult RedirectAfterCourseAction(int id, string? returnUrl)
    {
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
