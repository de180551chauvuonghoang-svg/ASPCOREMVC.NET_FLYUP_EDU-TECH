using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

[Authorize(Roles = "Admin,Instructor")]
public class InstructorController : Controller
{
    private readonly ICourseService _courseService;
    private readonly ICourseAccessService _courseAccess;

    public InstructorController(ICourseService courseService, ICourseAccessService courseAccess)
    {
        _courseService = courseService;
        _courseAccess = courseAccess;
    }

    // GET: /Instructor/Dashboard or /Instructor
    [HttpGet]
    public async Task<IActionResult> Dashboard(string? search, string? level, string? status, string? category)
    {
        var isAdmin = User.IsInRole("Admin");

        var allCourses = (await _courseService.GetAllCoursesAsync()).ToList();

        var instructorCourses = allCourses.Where(c => _courseAccess.CanManageCourse(c, User)).ToList();

        var filtered = instructorCourses.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            filtered = filtered.Where(c => c.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) || 
                                           c.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(level) && Enum.TryParse<CourseLevel>(level, true, out var parsedLevel))
        {
            filtered = filtered.Where(c => c.Level == parsedLevel);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<CourseStatus>(status, true, out var parsedStatus))
            {
                filtered = filtered.Where(c => c.Status == parsedStatus);
            }
            else if (status.Equals("published", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(c => c.Status == CourseStatus.Approved || c.IsPublished);
            }
            else if (status.Equals("draft", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(c => c.Status == CourseStatus.Draft || !c.IsPublished);
            }
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            filtered = filtered.Where(c => string.Equals(c.Category, category.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var courseList = filtered.OrderByDescending(c => c.CreatedAt).ToList();

        // Statistics
        ViewBag.TotalCourses = instructorCourses.Count;
        ViewBag.ApprovedCourses = instructorCourses.Count(c => c.Status == CourseStatus.Approved || c.IsPublished);
        ViewBag.PendingApprovalCourses = instructorCourses.Count(c => c.Status == CourseStatus.PendingApproval);
        ViewBag.DraftCourses = instructorCourses.Count(c => c.Status == CourseStatus.Draft && !c.IsPublished);
        ViewBag.RejectedCourses = instructorCourses.Count(c => c.Status == CourseStatus.Rejected);
        ViewBag.ArchivedCourses = instructorCourses.Count(c => c.Status == CourseStatus.Archived);
        ViewBag.TotalLessons = instructorCourses.Sum(c => c.Lessons?.Count ?? 0);

        ViewBag.Categories = (await _courseService.GetCategoriesAsync()).ToList();

        ViewBag.CurrentSearch = search;
        ViewBag.CurrentLevel = level;
        ViewBag.CurrentStatus = status;
        ViewBag.CurrentCategory = category;
        ViewBag.IsAdmin = isAdmin;

        return View(courseList);
    }

    [HttpGet]
    public IActionResult Index() => RedirectToAction(nameof(Dashboard));

    // POST: /Instructor/SubmitApproval/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitApproval(int id)
    {
        var course = await _courseService.GetCourseByIdAsync(id);
        if (course == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy khóa học.";
            return RedirectToAction(nameof(Dashboard));
        }

        if (!_courseAccess.CanManageCourse(course, User))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền gửi duyệt khóa học này.";
            return RedirectToAction(nameof(Dashboard));
        }

        var (success, message) = await _courseService.SubmitCourseForApprovalAsync(id, course.InstructorId);
        if (success)
        {
            TempData["SuccessMessage"] = message;
        }
        else
        {
            TempData["ErrorMessage"] = message;
        }

        return RedirectToAction(nameof(Dashboard));
    }

    // POST: /Instructor/Archive/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id)
    {
        var course = await _courseService.GetCourseByIdAsync(id);
        if (course == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy khóa học.";
            return RedirectToAction(nameof(Dashboard));
        }

        if (!_courseAccess.CanManageCourse(course, User))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền thao tác trên khóa học này.";
            return RedirectToAction(nameof(Dashboard));
        }

        var (success, message) = await _courseService.ArchiveCourseAsync(id, course.InstructorId);
        if (success)
        {
            TempData["SuccessMessage"] = message;
        }
        else
        {
            TempData["ErrorMessage"] = message;
        }

        return RedirectToAction(nameof(Dashboard));
    }

    // POST: /Instructor/Unarchive/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unarchive(int id)
    {
        var course = await _courseService.GetCourseByIdAsync(id);
        if (course == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy khóa học.";
            return RedirectToAction(nameof(Dashboard));
        }

        if (!_courseAccess.CanManageCourse(course, User))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền thao tác trên khóa học này.";
            return RedirectToAction(nameof(Dashboard));
        }

        var (success, message) = await _courseService.UnarchiveCourseAsync(id, course.InstructorId);
        if (success)
        {
            TempData["SuccessMessage"] = message;
        }
        else
        {
            TempData["ErrorMessage"] = message;
        }

        return RedirectToAction(nameof(Dashboard));
    }
}
