using EduFlyUp.Domain.Entities;
using EduFlyUp.Domain.Interfaces;
using EduFlyUp.Web.Models.Courses;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

public class CoursesController : Controller
{
    private readonly IUnitOfWork _unitOfWork;

    public CoursesController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    // GET: /Courses
    public async Task<IActionResult> Index()
    {
        var courses = await _unitOfWork.Courses.GetPublishedCoursesAsync();

        var viewModel = courses.Select(c => new CourseItemModel
        {
            Id = c.Id,
            Title = c.Title,
            Description = c.Description,
            ThumbnailUrl = string.IsNullOrEmpty(c.ThumbnailUrl) ? "https://placehold.co/600x400/4f46e5/ffffff?text=Course" : c.ThumbnailUrl,
            Price = c.Price,
            Level = c.Level,
            LessonCount = c.Lessons.Count
        }).ToList();

        return View(viewModel);
    }

    // GET: /Courses/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var course = await _unitOfWork.Courses.GetCourseWithLessonsAsync(id);
        if (course == null)
        {
            return NotFound();
        }

        var viewModel = new CourseDetailViewModel
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            ThumbnailUrl = string.IsNullOrEmpty(course.ThumbnailUrl) ? "https://placehold.co/600x400/4f46e5/ffffff?text=Course" : course.ThumbnailUrl,
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
    public IActionResult Create()
    {
        return View(new CourseCreateModel());
    }

    // POST: /Courses/Create
    [HttpPost]
    [ValidateAntiForgeryToken] // Chống tấn công CSRF
    public async Task<IActionResult> Create(CourseCreateModel model)
    {
        // 1. Kiểm tra Server-side validation
        if (!ModelState.IsValid)
        {
            return View(model); // Trả lại form kèm thông báo lỗi
        }

        // 2. Map ViewModel ──► Domain Entity
        var course = new Course
        {
            Title = model.Title,
            Description = model.Description,
            ThumbnailUrl = model.ThumbnailUrl ?? string.Empty,
            Price = model.Price,
            Level = model.Level,
            IsPublished = model.IsPublished,
            InstructorId = "instructor-system-default"
        };

        // 3. Lưu vào Database qua Unit of Work
        await _unitOfWork.Courses.AddAsync(course);
        await _unitOfWork.SaveChangesAsync();

        // 4. Flash message và chuyển hướng
        TempData["SuccessMessage"] = $"Khóa học '{course.Title}' đã được tạo thành công!";
        return RedirectToAction(nameof(Index));
    }

    // ──────────────────────────────────────────────────────────────
    // EDIT — Kiến thức: GET/POST separation, ViewModel cho Edit form
    // ──────────────────────────────────────────────────────────────

    // GET: /Courses/Edit/5
    // Load form chỉnh sửa, điền sẵn dữ liệu hiện tại của Course
    public async Task<IActionResult> Edit(int id)
    {
        var course = await _unitOfWork.Courses.GetByIdAsync(id);
        if (course == null)
        {
            return NotFound();
        }

        // Map Entity → EditModel (chỉ expose field được phép sửa)
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
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CourseEditModel model)
    {
        // Kiểm tra Id khớp (tránh tampering)
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var course = await _unitOfWork.Courses.GetByIdAsync(id);
        if (course == null)
        {
            return NotFound();
        }

        // Chỉ cập nhật các field cho phép — InstructorId, CreatedAt KHÔNG bị thay đổi
        course.Title = model.Title;
        course.Description = model.Description;
        course.ThumbnailUrl = model.ThumbnailUrl ?? string.Empty;
        course.Price = model.Price;
        course.Level = model.Level;
        course.IsPublished = model.IsPublished;

        _unitOfWork.Courses.Update(course);
        await _unitOfWork.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Khóa học '{course.Title}' đã được cập nhật thành công!";
        return RedirectToAction(nameof(Details), new { id = course.Id });
    }

    // ──────────────────────────────────────────────────────────────
    // DELETE — Kiến thức: POST-only delete, chống CSRF
    // ──────────────────────────────────────────────────────────────

    // POST: /Courses/Delete/5
    // Chỉ cho phép DELETE qua HTTP POST (không cho phép GET vì link có thể bị click nhầm)
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var course = await _unitOfWork.Courses.GetByIdAsync(id);
        if (course == null)
        {
            return NotFound();
        }

        _unitOfWork.Courses.Delete(course);
        await _unitOfWork.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Khóa học '{course.Title}' đã bị xóa.";
        return RedirectToAction(nameof(Index));
    }
}

