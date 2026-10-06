using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

[Authorize(Roles = "Admin,Instructor")]
public class InstructorController : Controller
{
    private readonly ICourseService _courseService;
    private readonly ILessonService _lessonService;

    public InstructorController(ICourseService courseService, ILessonService lessonService)
    {
        _courseService = courseService;
        _lessonService = lessonService;
    }

    // GET: /Instructor/Dashboard or /Instructor
    [HttpGet]
    public async Task<IActionResult> Dashboard(string? search, string? level, string? status)
    {
        var userName = User.Identity?.Name ?? string.Empty;
        var isAdmin = User.IsInRole("Admin");

        var allCourses = (await _courseService.GetAllCoursesAsync()).ToList();

        // Admin can manage all; Instructor manages their courses (or default instructor courses)
        var instructorCourses = isAdmin
            ? allCourses
            : allCourses.Where(c => string.Equals(c.InstructorId, userName, StringComparison.OrdinalIgnoreCase) 
                                 || c.InstructorId == "instructor@eduflyup.vn" 
                                 || c.InstructorId == "seed-instructor-1").ToList();

        if (!isAdmin && !instructorCourses.Any())
        {
            instructorCourses = allCourses;
        }

        var filtered = instructorCourses.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(c => c.Title.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(level) && Enum.TryParse<CourseLevel>(level, out var parsedLevel))
        {
            filtered = filtered.Where(c => c.Level == parsedLevel);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status.Equals("published", StringComparison.OrdinalIgnoreCase))
                filtered = filtered.Where(c => c.IsPublished);
            else if (status.Equals("draft", StringComparison.OrdinalIgnoreCase))
                filtered = filtered.Where(c => !c.IsPublished);
        }

        var courseList = filtered.OrderByDescending(c => c.CreatedAt).ToList();

        // Statistics
        ViewBag.TotalCourses = instructorCourses.Count;
        ViewBag.PublishedCourses = instructorCourses.Count(c => c.IsPublished);
        ViewBag.DraftCourses = instructorCourses.Count(c => !c.IsPublished);
        ViewBag.TotalLessons = instructorCourses.Sum(c => c.Lessons?.Count ?? 0);

        ViewBag.CurrentSearch = search;
        ViewBag.CurrentLevel = level;
        ViewBag.CurrentStatus = status;
        ViewBag.IsAdmin = isAdmin;

        return View(courseList);
    }

    [HttpGet]
    public IActionResult Index() => RedirectToAction(nameof(Dashboard));
}
