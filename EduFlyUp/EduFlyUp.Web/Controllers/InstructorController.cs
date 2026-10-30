using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using EduFlyUp.Web.Models.Instructor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduFlyUp.Web.Controllers;

[Authorize(Roles = "Admin,Instructor")]
public class InstructorController : Controller
{
    private readonly ICourseService _courseService;
    private readonly ICourseAccessService _courseAccess;
    private readonly ILessonService _lessonService;
    private readonly EduFlyUp.Web.Storage.LessonFileCleanup _fileCleanup;
    private readonly IInstructorStudentService _students;

    public InstructorController(ICourseService courseService, ICourseAccessService courseAccess, ILessonService lessonService, EduFlyUp.Web.Storage.LessonFileCleanup fileCleanup, IInstructorStudentService students)
    {
        _courseService = courseService;
        _courseAccess = courseAccess;
        _lessonService = lessonService;
        _fileCleanup = fileCleanup;
        _students = students;
    }

    // GET: /Instructor/Dashboard or /Instructor
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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
        if (!isAdmin)
        {
            ViewData["EnrollmentSummary"] = await _students.SummaryAsync(new InstructorIdentity(
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", User.Identity?.Name ?? ""));
        }

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

    // ==========================================
    // KHỐI 2: QUẢN LÝ GIÁO TRÌNH & BÀI GIẢNG (CURRICULUM BUILDER - UC-21, UC-24)
    // ==========================================

    // GET: /Instructor/Curriculum/5
    [HttpGet]
    public async Task<IActionResult> Curriculum(int id)
    {
        var course = await _courseService.GetCourseByIdAsync(id);
        if (course == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy khóa học.";
            return RedirectToAction(nameof(Dashboard));
        }

        if (!_courseAccess.CanManageCourse(course, User))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền quản lý khóa học này.";
            return RedirectToAction(nameof(Dashboard));
        }

        var lessons = (await _lessonService.GetLessonsByCourseIdAsync(id))
            .OrderBy(l => l.Order)
            .ToList();

        var sections = lessons
            .GroupBy(l => string.IsNullOrWhiteSpace(l.SectionName) ? "Chương 1: Tổng quan" : l.SectionName.Trim())
            .ToDictionary(g => g.Key, g => g.ToList());

        var model = new CurriculumViewModel
        {
            Course = course,
            Lessons = lessons,
            Sections = sections
        };

        return View(model);
    }

    // POST: /Instructor/CreateLesson
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLesson(LessonInputModel model)
    {
        var course = await _courseService.GetCourseByIdAsync(model.CourseId);
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

        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            TempData["ErrorMessage"] = string.IsNullOrEmpty(firstError) ? "Thông tin bài giảng không hợp lệ. Vui lòng kiểm tra lại." : firstError;
            return RedirectToAction(nameof(Curriculum), new { id = model.CourseId });
        }

        var existingLessons = (await _lessonService.GetLessonsByCourseIdAsync(model.CourseId)).ToList();
        var nextOrder = model.Order > 0 ? model.Order : (existingLessons.Count + 1);

        var lesson = new Lesson
        {
            CourseId = model.CourseId,
            Title = model.Title.Trim(),
            SectionName = string.IsNullOrWhiteSpace(model.SectionName) ? "Chương 1: Tổng quan" : model.SectionName.Trim(),
            VideoUrl = model.VideoUrl?.Trim() ?? string.Empty,
            Content = model.Content?.Trim() ?? string.Empty,
            DurationMinutes = model.DurationMinutes,
            Order = nextOrder,
            IsPreview = model.IsPreview,
            MaterialUrl = model.MaterialUrl?.Trim() ?? string.Empty,
            AttachmentName = model.AttachmentName?.Trim() ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        await _lessonService.CreateLessonAsync(lesson);
        TempData["SuccessMessage"] = $"Đã thêm bài giảng \"{lesson.Title}\" thành công.";
        return RedirectToAction(nameof(Curriculum), new { id = model.CourseId });
    }

    // POST: /Instructor/EditLesson
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditLesson(LessonInputModel model)
    {
        var lesson = await _lessonService.GetLessonByIdAsync(model.Id);
        if (lesson == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy bài giảng cần chỉnh sửa.";
            return RedirectToAction(nameof(Curriculum), new { id = model.CourseId });
        }

        var course = await _courseService.GetCourseByIdAsync(lesson.CourseId);
        if (course == null || !_courseAccess.CanManageCourse(course, User))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa bài giảng của khóa học này.";
            return RedirectToAction(nameof(Dashboard));
        }

        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            TempData["ErrorMessage"] = string.IsNullOrEmpty(firstError) ? "Thông tin chỉnh sửa không hợp lệ." : firstError;
            return RedirectToAction(nameof(Curriculum), new { id = lesson.CourseId });
        }

        lesson.Title = model.Title.Trim();
        lesson.SectionName = string.IsNullOrWhiteSpace(model.SectionName) ? "Chương 1: Tổng quan" : model.SectionName.Trim();
        if (!lesson.VideoUrl.StartsWith("/LessonVideos/Stream/", StringComparison.Ordinal))
            lesson.VideoUrl = model.VideoUrl?.Trim() ?? string.Empty;
        lesson.Content = model.Content?.Trim() ?? string.Empty;
        lesson.DurationMinutes = model.DurationMinutes;
        lesson.Order = model.Order;
        lesson.IsPreview = model.IsPreview;
        // Uploaded files are managed separately from external links.
        if (!lesson.MaterialUrl.StartsWith("/LessonMaterials/Download/", StringComparison.Ordinal))
        {
            lesson.MaterialUrl = model.MaterialUrl?.Trim() ?? string.Empty;
            lesson.AttachmentName = model.AttachmentName?.Trim() ?? string.Empty;
        }
        lesson.UpdatedAt = DateTime.UtcNow;

        await _lessonService.UpdateLessonAsync(lesson);
        TempData["SuccessMessage"] = $"Đã cập nhật bài giảng \"{lesson.Title}\" thành công.";
        return RedirectToAction(nameof(Curriculum), new { id = lesson.CourseId });
    }

    // POST: /Instructor/DeleteLesson
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteLesson(int id, int courseId)
    {
        var lesson = await _lessonService.GetLessonByIdAsync(id);
        if (lesson == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy bài giảng cần xóa.";
            return RedirectToAction(nameof(Curriculum), new { id = courseId });
        }

        var course = await _courseService.GetCourseByIdAsync(lesson.CourseId);
        if (course == null || !_courseAccess.CanManageCourse(course, User))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền xóa bài giảng của khóa học này.";
            return RedirectToAction(nameof(Dashboard));
        }

        try
        {
            var deleted = await _lessonService.DeleteLessonAsync(id);
            if (deleted is not null) _fileCleanup.DeleteFiles(deleted);
            TempData["SuccessMessage"] = "Đã xóa bài giảng thành công.";
        }
        catch (Exception)
        {
            TempData["ErrorMessage"] = "Không thể xóa bài giảng này do có dữ liệu tiến độ học tập hoặc ràng buộc liên quan.";
        }

        return RedirectToAction(nameof(Curriculum), new { id = courseId });
    }
}
