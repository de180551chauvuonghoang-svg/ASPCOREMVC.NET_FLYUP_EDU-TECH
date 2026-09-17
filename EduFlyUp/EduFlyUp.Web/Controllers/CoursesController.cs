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
}
