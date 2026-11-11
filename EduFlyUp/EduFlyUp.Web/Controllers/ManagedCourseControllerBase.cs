using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

/// <summary>
/// Controller cơ sở dùng chung helper phân quyền và kiểm tra khóa học cho khu vực Giảng viên / Quản trị nội dung.
/// </summary>
public abstract class ManagedCourseControllerBase : Controller
{
    protected async Task<(Course? Course, IActionResult? ErrorResult)> AuthorizeManagedCourseAsync(
        ICourseService courseService,
        ICourseAccessService courseAccess,
        int courseId,
        string unauthorizedMessage = "Bạn không có quyền quản lý khóa học này.",
        string invalidIdMessage = "Mã khóa học không hợp lệ.",
        string notFoundMessage = "Không tìm thấy khóa học.")
    {
        if (courseId <= 0)
        {
            TempData["ErrorMessage"] = invalidIdMessage;
            return (null, RedirectToAction("Dashboard", "Instructor"));
        }

        var course = await courseService.GetCourseByIdAsync(courseId);
        if (course == null)
        {
            TempData["ErrorMessage"] = notFoundMessage;
            return (null, RedirectToAction("Dashboard", "Instructor"));
        }

        if (!courseAccess.CanManageCourse(course, User))
        {
            TempData["ErrorMessage"] = unauthorizedMessage;
            return (null, RedirectToAction("Dashboard", "Instructor"));
        }

        return (course, null);
    }
}

