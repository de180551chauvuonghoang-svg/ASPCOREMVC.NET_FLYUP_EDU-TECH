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

    public CoursesController(ICourseService courseService, ICourseAccessService courseAccess)
    {
        _courseService = courseService;
        _courseAccess = courseAccess;
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

    // GET: /Courses/Create
    // Chỉ Admin và Instructor mới có quyền tạo khóa học mới
    [Authorize(Roles = "Admin,Instructor")]
    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateCourseModel { IsPublished = User.IsInRole("Admin") });
    }

    // POST: /Courses/Create
    [HttpPost]
    [Authorize(Roles = "Admin,Instructor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCourseModel model)
    {
        if (string.IsNullOrWhiteSpace(User.Identity?.Name)) return Forbid();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var course = new Course
        {
            Title = model.Title,
            Description = model.Description,
            ThumbnailUrl = model.ThumbnailUrl ?? string.Empty,
            Price = model.Price,
            Level = model.Level,
            IsPublished = User.IsInRole("Admin") && model.IsPublished,
            InstructorId = User.Identity!.Name!
        };

        await _courseService.CreateCourseAsync(course);

        TempData["SuccessMessage"] = $"Khóa học '{course.Title}' đã được tạo thành công!";
        if (!User.IsInRole("Admin")) return RedirectToAction("Dashboard", "Instructor");
        return RedirectToAction(nameof(Index));
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

        var model = new EditCourseModel
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            ThumbnailUrl = course.ThumbnailUrl,
            Price = course.Price,
            Level = course.Level,
            IsPublished = course.IsPublished
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

        if (!ModelState.IsValid) return View(model);

        course.Title = model.Title;
        course.Description = model.Description;
        course.ThumbnailUrl = model.ThumbnailUrl ?? string.Empty;
        course.Price = model.Price;
        course.Level = model.Level;
        if (User.IsInRole("Admin")) course.IsPublished = model.IsPublished;

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

        try
        {
            await _courseService.DeleteCourseAsync(id);
            TempData["SuccessMessage"] = $"Khóa học '{course.Title}' đã bị xóa.";
        }
        catch (Exception)
        {
            TempData["ErrorMessage"] = "Không thể xóa khóa học do đã có dữ liệu liên quan (học viên đăng ký, bài giảng hoặc bài thi). Bạn có thể chọn Lưu trữ khóa học thay thế.";
        }

        if (!User.IsInRole("Admin")) return RedirectToAction("Dashboard", "Instructor");
        return RedirectToAction(nameof(Index));
    }
}
