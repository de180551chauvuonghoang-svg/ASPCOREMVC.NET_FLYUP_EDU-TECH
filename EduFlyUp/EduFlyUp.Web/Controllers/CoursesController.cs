using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using EduFlyUp.Web.Models.Courses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

public class CoursesController : Controller
{
    private readonly ICourseService _courseService;

    public CoursesController(ICourseService courseService)
    {
        _courseService = courseService;
    }

    // GET: /Courses
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
    public async Task<IActionResult> Details(int id)
    {
        var course = await _courseService.GetCourseByIdAsync(id);
        if (course == null)
        {
            return NotFound();
        }

        var viewModel = new CourseDetailViewModel
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            ThumbnailUrl = string.IsNullOrEmpty(course.ThumbnailUrl) ? "https://images.unsplash.com/photo-1517694712202-14dd9538aa97?w=600&auto=format&fit=crop&q=80" : course.ThumbnailUrl,
            Price = course.Price,
            Level = course.Level,
            CreatedAt = course.CreatedAt,
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
    public IActionResult Create()
    {
        return View(new CourseCreateModel());
    }

    // POST: /Courses/Create
    [HttpPost]
    [Authorize(Roles = "Admin,Instructor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CourseCreateModel model)
    {
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
            IsPublished = model.IsPublished,
            InstructorId = User.Identity?.Name ?? "instructor@eduflyup.vn"
        };

        await _courseService.CreateCourseAsync(course);

        TempData["SuccessMessage"] = $"Khóa học '{course.Title}' đã được tạo thành công!";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Courses/Edit/5
    [Authorize(Roles = "Admin,Instructor")]
    public async Task<IActionResult> Edit(int id)
    {
        var course = await _courseService.GetCourseByIdAsync(id);
        if (course == null)
        {
            return NotFound();
        }

        var model = new CourseEditModel
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
    public async Task<IActionResult> Edit(int id, CourseEditModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var course = await _courseService.GetCourseByIdAsync(id);
        if (course == null)
        {
            return NotFound();
        }

        course.Title = model.Title;
        course.Description = model.Description;
        course.ThumbnailUrl = model.ThumbnailUrl ?? string.Empty;
        course.Price = model.Price;
        course.Level = model.Level;
        course.IsPublished = model.IsPublished;

        await _courseService.UpdateCourseAsync(course);

        TempData["SuccessMessage"] = $"Khóa học '{course.Title}' đã được cập nhật thành công!";
        return RedirectToAction(nameof(Details), new { id = course.Id });
    }

    // POST: /Courses/Delete/5
    [HttpPost, ActionName("Delete")]
    [Authorize(Roles = "Admin,Instructor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var course = await _courseService.GetCourseByIdAsync(id);
        if (course == null)
        {
            return NotFound();
        }

        await _courseService.DeleteCourseAsync(id);

        TempData["SuccessMessage"] = $"Khóa học '{course.Title}' đã bị xóa.";
        return RedirectToAction(nameof(Index));
    }
}
