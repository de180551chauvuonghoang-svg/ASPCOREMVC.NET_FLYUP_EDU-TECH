using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;
using EduFlyUp.Services;
using EduFlyUp.Web.Models.Instructor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduFlyUp.Web.Controllers;

[Authorize(Roles = "Admin,Instructor")]
public class InstructorController : ManagedCourseControllerBase
{
    private readonly ICourseService _courseService;
    private readonly ICourseAccessService _courseAccess;
    private readonly ILessonService _lessonService;
    private readonly EduFlyUp.Web.Storage.LessonFileCleanup _fileCleanup;
    private readonly IInstructorStudentService _students;
    private readonly IEnrollmentRepository _enrollments;

    public InstructorController(
        ICourseService courseService,
        ICourseAccessService courseAccess,
        ILessonService lessonService,
        EduFlyUp.Web.Storage.LessonFileCleanup fileCleanup,
        IInstructorStudentService students,
        IEnrollmentRepository enrollments)
    {
        _courseService = courseService;
        _courseAccess = courseAccess;
        _lessonService = lessonService;
        _fileCleanup = fileCleanup;
        _students = students;
        _enrollments = enrollments;
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
                filtered = filtered.Where(c => c.GetEffectiveStatus() == parsedStatus);
            }
            else if (status.Equals("published", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(c => c.GetEffectiveStatus() == CourseStatus.Approved);
            }
            else if (status.Equals("draft", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(c => c.GetEffectiveStatus() == CourseStatus.Draft);
            }
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            filtered = filtered.Where(c => string.Equals(c.Category, category.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var courseList = filtered.OrderByDescending(c => c.CreatedAt).ToList();

        // Statistics
        ViewBag.TotalCourses = instructorCourses.Count;
        ViewBag.ApprovedCourses = instructorCourses.Count(c => c.GetEffectiveStatus() == CourseStatus.Approved);
        ViewBag.PendingApprovalCourses = instructorCourses.Count(c => c.GetEffectiveStatus() == CourseStatus.PendingApproval);
        ViewBag.DraftCourses = instructorCourses.Count(c => c.GetEffectiveStatus() == CourseStatus.Draft);
        ViewBag.RejectedCourses = instructorCourses.Count(c => c.GetEffectiveStatus() == CourseStatus.Rejected);
        ViewBag.ArchivedCourses = instructorCourses.Count(c => c.GetEffectiveStatus() == CourseStatus.Archived);
        ViewBag.TotalLessons = instructorCourses.Sum(c => c.Lessons?.Count ?? 0);
        if (!isAdmin)
        {
            var instructor = new InstructorIdentity(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", User.Identity?.Name ?? "");
            ViewData["EnrollmentSummary"] = await _students.SummaryAsync(instructor);
            ViewData["PopularCourses"] = await _students.PopularCoursesAsync(instructor);
        }

        ViewBag.Categories = (await _courseService.GetCategoriesAsync()).ToList();
        ViewBag.CourseEnrollmentCounts = await _enrollments.GetCourseEnrollmentCountsAsync(instructorCourses.Select(c => c.Id));

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
    public Task<IActionResult> SubmitApproval(int id)
        => ExecuteCourseWorkflowActionAsync(id, _courseService.SubmitCourseForApprovalAsync, "Bạn không có quyền gửi duyệt khóa học này.");

    // POST: /Instructor/Archive/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Archive(int id)
        => ExecuteCourseWorkflowActionAsync(id, _courseService.ArchiveCourseAsync, "Bạn không có quyền thao tác trên khóa học này.");

    // POST: /Instructor/Unarchive/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Unarchive(int id)
        => ExecuteCourseWorkflowActionAsync(id, _courseService.UnarchiveCourseAsync, "Bạn không có quyền thao tác trên khóa học này.");

    private async Task<IActionResult> ExecuteCourseWorkflowActionAsync(
        int id,
        Func<int, string, Task<(bool Success, string Message)>> workflowAction,
        string unauthorizedMessage)
    {
        var (course, errorResult) = await AuthorizeManagedCourseAsync(_courseService, _courseAccess, id, unauthorizedMessage);
        if (errorResult != null || course == null) return errorResult!;

        var (success, message) = await workflowAction(id, course.InstructorId);
        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction(nameof(Dashboard));
    }

    // ==========================================
    // KHỐI 2: QUẢN LÝ GIÁO TRÌNH & BÀI GIẢNG
    // ==========================================

    // GET: /Instructor/Curriculum/5
    [HttpGet]
    public async Task<IActionResult> Curriculum(int id)
    {
        var (course, errorResult) = await AuthorizeManagedCourseAsync(_courseService, _courseAccess, id, "Bạn không có quyền quản lý khóa học này.");
        if (errorResult != null || course == null) return errorResult!;

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

        ViewBag.HasActiveEnrollments = await _enrollments.HasActiveCourseEnrollmentsAsync(course.Id);
        return View(model);
    }

    // POST: /Instructor/CreateLesson
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLesson(LessonInputModel model)
    {
        var (course, errorResult) = await AuthorizeManagedCourseAsync(_courseService, _courseAccess, model.CourseId, "Bạn không có quyền thao tác trên khóa học này.");
        if (errorResult != null || course == null) return errorResult!;

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

        var (course, errorResult) = await AuthorizeManagedCourseAsync(_courseService, _courseAccess, lesson.CourseId, "Bạn không có quyền chỉnh sửa bài giảng của khóa học này.");
        if (errorResult != null || course == null) return errorResult!;

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

        var (course, errorResult) = await AuthorizeManagedCourseAsync(_courseService, _courseAccess, lesson.CourseId, "Bạn không có quyền xóa bài giảng của khóa học này.");
        if (errorResult != null || course == null) return errorResult!;

        var targetCourseId = course.Id;
        var existingLessons = (await _lessonService.GetLessonsByCourseIdAsync(targetCourseId)).ToList();
        if (existingLessons.Count <= 1)
        {
            var hasActiveEnrollments = await _enrollments.HasActiveCourseEnrollmentsAsync(targetCourseId);
            string? blockReason = course.IsPublished
                ? "Không thể xóa bài giảng duy nhất của khóa học đang công khai mở bán. Vui lòng lưu trữ (tạm ẩn) khóa học trước khi điều chỉnh."
                : course.Status == CourseStatus.PendingApproval
                    ? "Không thể xóa bài giảng duy nhất khi khóa học đang chờ Admin duyệt."
                    : hasActiveEnrollments
                        ? "Không thể xóa bài giảng duy nhất vì khóa học đã có học viên ghi danh đang học tập."
                        : course.Status == CourseStatus.Archived
                            ? "Không thể xóa bài giảng duy nhất của khóa học đang lưu trữ. Hãy thêm bài giảng mới trước khi xóa bài giảng này."
                            : null;

            if (blockReason != null)
            {
                TempData["ErrorMessage"] = blockReason;
                return RedirectToAction(nameof(Curriculum), new { id = targetCourseId });
            }
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

        return RedirectToAction(nameof(Curriculum), new { id = targetCourseId });
    }
}
