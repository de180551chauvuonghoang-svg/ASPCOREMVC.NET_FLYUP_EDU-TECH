using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using EduFlyUp.Web.Models.Courses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

public class CoursesController : Controller
{
    private readonly ICourseService _courseService;
    private readonly ICourseAccessService _courseAccess;
    private readonly EduFlyUp.Web.Storage.LessonFileCleanup _fileCleanup;

    public CoursesController(ICourseService courseService, ICourseAccessService courseAccess, EduFlyUp.Web.Storage.LessonFileCleanup fileCleanup)
    {
        _courseService = courseService;
        _courseAccess = courseAccess;
        _fileCleanup = fileCleanup;
    }

    // GET: /Courses
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var courses = await _courseService.GetPublishedCoursesAsync();

        var viewModel = courses.Select(c => new CourseItemModel
        {
            Id = c.Id,
            Title = c.Title,
            Description = c.Description,
            ThumbnailUrl = string.IsNullOrEmpty(c.ThumbnailUrl) ? "https://images.unsplash.com/photo-1517694712202-14dd9538aa97?w=600&auto=format&fit=crop&q=80" : c.ThumbnailUrl,
            Price = c.Price,
            Level = c.Level,
            LessonCount = c.Lessons.Count
        }).ToList();

        return View(viewModel);
    }

    // GET: /Courses/Details/5
    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] int id)
    {
        var course = await _courseService.GetCourseByIdAsync(id);
        if (course == null)
        {
            return NotFound();
        }

        if (!await _courseAccess.CanViewCourseAsync(course, User)) return NotFound();

        var viewModel = new DetailCourseModel
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            ThumbnailUrl = string.IsNullOrEmpty(course.ThumbnailUrl) ? "https://images.unsplash.com/photo-1517694712202-14dd9538aa97?w=600&auto=format&fit=crop&q=80" : course.ThumbnailUrl,
            Price = course.Price,
            Level = course.Level,
            CreatedAt = course.CreatedAt,
            CanManage = _courseAccess.CanManageCourse(course, User),
            Lessons = course.Lessons.OrderBy(l => l.Order).Select(l => new LessonItemViewModel
            {
                Id = l.Id,
                Title = l.Title,
                DurationMinutes = l.DurationMinutes,
                Order = l.Order,
                IsPreview = l.IsPreview
            }).ToList()
        };

        return View(viewModel);
    }

    private static readonly string[] DefaultCategories =
    [
        "Công nghệ thông tin",
        "Lập trình C#",
        "ASP.NET Core",
        "Cơ sở dữ liệu",
        "Frontend & UI",
        "Kiến trúc phần mềm",
        "Khác"
    ];

    private async Task PopulateCategorySuggestionsAsync()
    {
        var existing = await _courseService.GetCategoriesAsync();
        ViewBag.Categories = DefaultCategories
            .Concat(existing.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeCategory(string? category)
        => string.IsNullOrWhiteSpace(category) ? "Công nghệ thông tin" : category.Trim();

    // GET: /Courses/Create
    // Chỉ Admin và Instructor mới có quyền tạo khóa học mới
    [Authorize(Roles = "Admin,Instructor")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateCategorySuggestionsAsync();
        return View(new CreateCourseModel { IsPublished = false, Category = "Công nghệ thông tin" });
    }

    // POST: /Courses/Create
    [HttpPost]
    [Authorize(Roles = "Admin,Instructor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCourseModel model)
    {
        if (string.IsNullOrWhiteSpace(User.Identity?.Name)) return Forbid();

        if (User.IsInRole("Admin") && model.IsPublished)
        {
            ModelState.AddModelError(string.Empty, "Khóa học mới chưa có bài giảng nào nên chưa thể công khai ngay. Vui lòng tạo ở chế độ chưa công khai, thêm bài giảng rồi duyệt mở bán.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateCategorySuggestionsAsync();
            return View(model);
        }

        var course = new Course
        {
            Title = model.Title,
            Description = model.Description,
            ThumbnailUrl = model.ThumbnailUrl ?? string.Empty,
            Price = model.Price,
            Level = model.Level,
            Category = NormalizeCategory(model.Category),
            IsPublished = false,
            Status = CourseStatus.Draft,
            InstructorId = User.Identity!.Name!
        };

        await _courseService.CreateCourseAsync(course);

        TempData["SuccessMessage"] = $"Khóa học '{course.Title}' đã được tạo thành công!";
        if (!User.IsInRole("Admin")) return RedirectToAction("Dashboard", "Instructor");
        return RedirectToAction("Curriculum", "Instructor", new { id = course.Id });
    }

    // GET: /Courses/Edit/5
    [HttpGet]
    [Authorize(Roles = "Admin,Instructor")]
    public async Task<IActionResult> Edit([FromRoute] int id)
    {
        var course = await _courseService.GetCourseByIdAsync(id);
        if (course == null)
        {
            return NotFound();
        }

        if (!_courseAccess.CanManageCourse(course, User)) return Forbid();

        await PopulateCategorySuggestionsAsync();
        var (canDelete, deleteDisabledReason) = await _courseService.CanDeleteCourseAsync(id);
        var model = new EditCourseModel
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            ThumbnailUrl = course.ThumbnailUrl,
            Price = course.Price,
            Level = course.Level,
            Category = course.Category,
            IsPublished = course.IsPublished,
            CanDelete = canDelete,
            DeleteDisabledReason = deleteDisabledReason
        };

        return View(model);
    }

    // POST: /Courses/Edit/5
    [HttpPost]
    [Authorize(Roles = "Admin,Instructor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromRoute] int id, EditCourseModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var course = await _courseService.GetCourseByIdAsync(id);
        if (course == null)
        {
            return NotFound();
        }

        if (!_courseAccess.CanManageCourse(course, User)) return Forbid();

        if (!User.IsInRole("Admin"))
        {
            ModelState.Remove(nameof(model.IsPublished));
            model.IsPublished = course.IsPublished;
        }
        else if (model.IsPublished && !course.IsPublished)
        {
            if (course.Lessons.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Không thể công khai khóa học chưa có bài giảng nào.");
            }
            else if (course.Status == CourseStatus.Draft)
            {
                ModelState.AddModelError(string.Empty, "Khóa học này đang là bản nháp, giảng viên chưa gửi yêu cầu phê duyệt.");
            }
            else if (course.Status == CourseStatus.Archived)
            {
                ModelState.AddModelError(string.Empty, "Khóa học này đã được giảng viên lưu trữ, không thể tự ý công khai.");
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateCategorySuggestionsAsync();
            var (canDelete, deleteDisabledReason) = await _courseService.CanDeleteCourseAsync(id);
            model.CanDelete = canDelete;
            model.DeleteDisabledReason = deleteDisabledReason;
            return View(model);
        }

        course.Title = model.Title;
        course.Description = model.Description;
        course.ThumbnailUrl = model.ThumbnailUrl ?? string.Empty;
        course.Price = model.Price;
        course.Level = model.Level;
        course.Category = NormalizeCategory(model.Category);
        if (User.IsInRole("Admin"))
        {
            course.IsPublished = model.IsPublished;
            if (course.IsPublished)
            {
                course.Status = CourseStatus.Approved;
                course.RejectionReason = null;
            }
            else if (course.Status is CourseStatus.Approved or CourseStatus.PendingApproval)
            {
                course.Status = CourseStatus.Rejected;
            }
        }

        if (User.IsInRole("Admin"))
            await _courseService.UpdateCourseAsync(course);
        else
            await _courseService.UpdateCourseContentAsync(course);

        TempData["SuccessMessage"] = $"Khóa học '{course.Title}' đã được cập nhật thành công!";
        return RedirectToAction(nameof(Details), new { id = course.Id });
    }

    // POST: /Courses/Delete/5
    [HttpPost, ActionName("Delete")]
    [Authorize(Roles = "Admin,Instructor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed([FromRoute] int id)
    {
        var course = await _courseService.GetCourseByIdAsync(id);
        if (course == null)
        {
            return NotFound();
        }

        if (!_courseAccess.CanManageCourse(course, User)) return Forbid();

        var (allowed, reason) = await _courseService.CanDeleteCourseAsync(id);
        if (!allowed)
        {
            TempData["ErrorMessage"] = reason ?? "Không thể xóa khóa học này.";
            if (!User.IsInRole("Admin")) return RedirectToAction("Dashboard", "Instructor");
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var deletedLessons = await _courseService.DeleteCourseAsync(id);
            foreach (var deleted in deletedLessons) _fileCleanup.DeleteFiles(deleted);
            TempData["SuccessMessage"] = $"Khóa học '{course.Title}' đã bị xóa.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (Exception)
        {
            TempData["ErrorMessage"] = "Không thể xóa khóa học do đã có dữ liệu liên quan (học viên đăng ký, bài giảng hoặc bài thi). Bạn có thể chọn Lưu trữ khóa học thay thế.";
        }

        if (!User.IsInRole("Admin")) return RedirectToAction("Dashboard", "Instructor");
        return RedirectToAction(nameof(Index));
    }
}
